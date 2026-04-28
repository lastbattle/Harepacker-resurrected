using HaCreator.MapSimulator.Character;
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
            public string SecondaryPassword { get; set; }
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
            public DateTime? ExpirationDateUtc { get; set; }
            public int? TotalUpgradeSlotCount { get; set; }
            public int? RemainingUpgradeSlotCount { get; set; }
            public int? EnhancementStarCount { get; set; }
            public string PotentialTierText { get; set; }
            public List<string> PotentialLines { get; set; }
            public List<int> ItemOptionIds { get; set; }
            public bool? HasGrowthInfo { get; set; }
            public int? GrowthLevel { get; set; }
            public int? GrowthMaxLevel { get; set; }
            public int? GrowthExpPercent { get; set; }
            public PersistedTooltipPartRecord TooltipPart { get; set; }
        }

        private sealed class PersistedTooltipPartRecord
        {
            public bool IsWeapon { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public string ItemCategory { get; set; }
            public bool IsCash { get; set; }
            public DateTime? ExpirationDateUtc { get; set; }
            public int? Durability { get; set; }
            public int? MaxDurability { get; set; }
            public int RequiredJobMask { get; set; }
            public int RequiredFame { get; set; }
            public int RequiredLevel { get; set; }
            public int RequiredSTR { get; set; }
            public int RequiredDEX { get; set; }
            public int RequiredINT { get; set; }
            public int RequiredLUK { get; set; }
            public int BonusSTR { get; set; }
            public int BonusDEX { get; set; }
            public int BonusINT { get; set; }
            public int BonusLUK { get; set; }
            public int BonusHP { get; set; }
            public int BonusMP { get; set; }
            public int BonusWeaponAttack { get; set; }
            public int BonusMagicAttack { get; set; }
            public int BonusWeaponDefense { get; set; }
            public int BonusMagicDefense { get; set; }
            public int BonusAccuracy { get; set; }
            public int BonusAvoidability { get; set; }
            public int BonusHands { get; set; }
            public int BonusSpeed { get; set; }
            public int BonusJump { get; set; }
            public int UpgradeSlots { get; set; }
            public int? TotalUpgradeSlotCount { get; set; }
            public int? RemainingUpgradeSlotCount { get; set; }
            public int EnhancementStarCount { get; set; }
            public int KnockbackRate { get; set; }
            public int TradeAvailable { get; set; }
            public bool IsTimeLimited { get; set; }
            public string PotentialTierText { get; set; }
            public List<string> PotentialLines { get; set; } = new();
            public List<int> ItemOptionIds { get; set; } = new();
            public bool HasGrowthInfo { get; set; }
            public int GrowthLevel { get; set; }
            public int GrowthMaxLevel { get; set; }
            public int GrowthExpPercent { get; set; }
            public int AttackSpeed { get; set; } = 6;
            public string WeaponType { get; set; }
        }

        public sealed class StorageAccountState
        {
            public string AccountLabel { get; init; }
            public int SlotLimit { get; init; } = 24;
            public long Meso { get; init; }
            public IReadOnlyList<string> AuthorizedCharacterNames { get; init; } = Array.Empty<string>();
            public string SecondaryPassword { get; init; }
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
            return GetStateByKey(ResolveAccountKey(accountLabel));
        }

        public StorageAccountState GetStateByKey(string accountKey)
        {
            string defaultAccountLabel = string.IsNullOrWhiteSpace(accountKey)
                ? "Simulator Account Storage"
                : accountKey.Trim();
            if (string.IsNullOrWhiteSpace(accountKey) || !_accountsByKey.TryGetValue(accountKey.Trim(), out PersistedStorageAccountState persisted))
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
                        Description = record.Description,
                        ExpirationDateUtc = record.ExpirationDateUtc,
                        TotalUpgradeSlotCount = record.TotalUpgradeSlotCount,
                        RemainingUpgradeSlotCount = record.RemainingUpgradeSlotCount,
                        EnhancementStarCount = record.EnhancementStarCount,
                        PotentialTierText = record.PotentialTierText,
                        PotentialLines = record.PotentialLines != null ? new List<string>(record.PotentialLines) : null,
                        ItemOptionIds = record.ItemOptionIds != null ? new List<int>(record.ItemOptionIds) : null,
                        HasGrowthInfo = record.HasGrowthInfo,
                        GrowthLevel = record.GrowthLevel,
                        GrowthMaxLevel = record.GrowthMaxLevel,
                        GrowthExpPercent = record.GrowthExpPercent,
                        TooltipPart = RestoreTooltipPart(record.ItemId, record.TooltipPart)
                    });
                }

                itemsByType[inventoryType] = rows;
            }

            return new StorageAccountState
            {
                AccountLabel = string.IsNullOrWhiteSpace(persisted.AccountLabel) ? defaultAccountLabel : persisted.AccountLabel,
                SlotLimit = Math.Max(24, persisted.SlotLimit),
                Meso = Math.Max(0, persisted.Meso),
                AuthorizedCharacterNames = NormalizeCharacterNames(persisted.AuthorizedCharacterNames),
                SecondaryPassword = persisted.SecondaryPassword ?? string.Empty,
                ItemsByType = itemsByType
            };
        }

        public void SaveState(
            string accountKey,
            string accountLabel,
            int slotLimit,
            long meso,
            IReadOnlyDictionary<InventoryType, List<InventorySlotData>> itemsByType,
            IReadOnlyCollection<string> authorizedCharacterNames = null,
            string secondaryPassword = null)
        {
            string normalizedLabel = string.IsNullOrWhiteSpace(accountLabel) ? "Simulator Account Storage" : accountLabel.Trim();
            string normalizedKey = string.IsNullOrWhiteSpace(accountKey)
                ? ResolveAccountKey(normalizedLabel)
                : accountKey.Trim();
            Dictionary<string, List<PersistedStorageSlotRecord>> persistedItems = BuildPersistedItems(itemsByType);

            _accountsByKey[normalizedKey] = new PersistedStorageAccountState
            {
                AccountLabel = normalizedLabel,
                SlotLimit = Math.Max(24, slotLimit),
                Meso = Math.Max(0, meso),
                AuthorizedCharacterNames = NormalizeCharacterNames(authorizedCharacterNames),
                SecondaryPassword = secondaryPassword ?? string.Empty,
                ItemsByType = persistedItems
            };

            SaveToDisk();
        }

        public void SaveState(
            string accountLabel,
            int slotLimit,
            long meso,
            IReadOnlyDictionary<InventoryType, List<InventorySlotData>> itemsByType,
            IReadOnlyCollection<string> authorizedCharacterNames = null,
            string secondaryPassword = null)
        {
            string normalizedLabel = string.IsNullOrWhiteSpace(accountLabel) ? "Simulator Account Storage" : accountLabel.Trim();
            SaveState(ResolveAccountKey(normalizedLabel), normalizedLabel, slotLimit, meso, itemsByType, authorizedCharacterNames, secondaryPassword);
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

        private static Dictionary<string, List<PersistedStorageSlotRecord>> BuildPersistedItems(IReadOnlyDictionary<InventoryType, List<InventorySlotData>> itemsByType)
        {
            Dictionary<string, List<PersistedStorageSlotRecord>> persistedItems = new(StringComparer.OrdinalIgnoreCase);
            if (itemsByType == null)
            {
                return persistedItems;
            }

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
                        Description = slot.Description,
                        ExpirationDateUtc = slot.ExpirationDateUtc,
                        TotalUpgradeSlotCount = slot.TotalUpgradeSlotCount,
                        RemainingUpgradeSlotCount = slot.RemainingUpgradeSlotCount,
                        EnhancementStarCount = slot.EnhancementStarCount,
                        PotentialTierText = slot.PotentialTierText,
                        PotentialLines = slot.PotentialLines != null ? new List<string>(slot.PotentialLines) : null,
                        ItemOptionIds = slot.ItemOptionIds != null ? new List<int>(slot.ItemOptionIds) : null,
                        HasGrowthInfo = slot.HasGrowthInfo,
                        GrowthLevel = slot.GrowthLevel,
                        GrowthMaxLevel = slot.GrowthMaxLevel,
                        GrowthExpPercent = slot.GrowthExpPercent,
                        TooltipPart = CaptureTooltipPart(slot.TooltipPart)
                    });
                }

                persistedItems[entry.Key.ToString()] = rows;
            }

            return persistedItems;
        }

        private static PersistedTooltipPartRecord CaptureTooltipPart(CharacterPart tooltipPart)
        {
            if (tooltipPart == null)
            {
                return null;
            }

            return new PersistedTooltipPartRecord
            {
                IsWeapon = tooltipPart is WeaponPart,
                Name = tooltipPart.Name,
                Description = tooltipPart.Description,
                ItemCategory = tooltipPart.ItemCategory,
                IsCash = tooltipPart.IsCash,
                ExpirationDateUtc = tooltipPart.ExpirationDateUtc,
                Durability = tooltipPart.Durability,
                MaxDurability = tooltipPart.MaxDurability,
                RequiredJobMask = tooltipPart.RequiredJobMask,
                RequiredFame = tooltipPart.RequiredFame,
                RequiredLevel = tooltipPart.RequiredLevel,
                RequiredSTR = tooltipPart.RequiredSTR,
                RequiredDEX = tooltipPart.RequiredDEX,
                RequiredINT = tooltipPart.RequiredINT,
                RequiredLUK = tooltipPart.RequiredLUK,
                BonusSTR = tooltipPart.BonusSTR,
                BonusDEX = tooltipPart.BonusDEX,
                BonusINT = tooltipPart.BonusINT,
                BonusLUK = tooltipPart.BonusLUK,
                BonusHP = tooltipPart.BonusHP,
                BonusMP = tooltipPart.BonusMP,
                BonusWeaponAttack = tooltipPart.BonusWeaponAttack,
                BonusMagicAttack = tooltipPart.BonusMagicAttack,
                BonusWeaponDefense = tooltipPart.BonusWeaponDefense,
                BonusMagicDefense = tooltipPart.BonusMagicDefense,
                BonusAccuracy = tooltipPart.BonusAccuracy,
                BonusAvoidability = tooltipPart.BonusAvoidability,
                BonusHands = tooltipPart.BonusHands,
                BonusSpeed = tooltipPart.BonusSpeed,
                BonusJump = tooltipPart.BonusJump,
                UpgradeSlots = tooltipPart.UpgradeSlots,
                TotalUpgradeSlotCount = tooltipPart.TotalUpgradeSlotCount,
                RemainingUpgradeSlotCount = tooltipPart.RemainingUpgradeSlotCount,
                EnhancementStarCount = tooltipPart.EnhancementStarCount,
                KnockbackRate = tooltipPart.KnockbackRate,
                TradeAvailable = tooltipPart.TradeAvailable,
                IsTimeLimited = tooltipPart.IsTimeLimited,
                PotentialTierText = tooltipPart.PotentialTierText,
                PotentialLines = tooltipPart.PotentialLines != null ? new List<string>(tooltipPart.PotentialLines) : new List<string>(),
                ItemOptionIds = tooltipPart.ItemOptionIds != null ? new List<int>(tooltipPart.ItemOptionIds) : new List<int>(),
                HasGrowthInfo = tooltipPart.HasGrowthInfo,
                GrowthLevel = tooltipPart.GrowthLevel,
                GrowthMaxLevel = tooltipPart.GrowthMaxLevel,
                GrowthExpPercent = tooltipPart.GrowthExpPercent,
                AttackSpeed = tooltipPart is WeaponPart weapon ? weapon.AttackSpeed : 6,
                WeaponType = tooltipPart is WeaponPart weaponPart ? weaponPart.WeaponType : null
            };
        }

        private static CharacterPart RestoreTooltipPart(int itemId, PersistedTooltipPartRecord persisted)
        {
            if (persisted == null)
            {
                return null;
            }

            CharacterPart tooltipPart = persisted.IsWeapon ? new WeaponPart() : new CharacterPart();
            tooltipPart.ItemId = itemId;
            tooltipPart.Name = persisted.Name;
            tooltipPart.Description = persisted.Description;
            tooltipPart.ItemCategory = persisted.ItemCategory;
            tooltipPart.IsCash = persisted.IsCash;
            tooltipPart.ExpirationDateUtc = persisted.ExpirationDateUtc;
            tooltipPart.Durability = persisted.Durability;
            tooltipPart.MaxDurability = persisted.MaxDurability;
            tooltipPart.RequiredJobMask = persisted.RequiredJobMask;
            tooltipPart.RequiredFame = persisted.RequiredFame;
            tooltipPart.RequiredLevel = persisted.RequiredLevel;
            tooltipPart.RequiredSTR = persisted.RequiredSTR;
            tooltipPart.RequiredDEX = persisted.RequiredDEX;
            tooltipPart.RequiredINT = persisted.RequiredINT;
            tooltipPart.RequiredLUK = persisted.RequiredLUK;
            tooltipPart.BonusSTR = persisted.BonusSTR;
            tooltipPart.BonusDEX = persisted.BonusDEX;
            tooltipPart.BonusINT = persisted.BonusINT;
            tooltipPart.BonusLUK = persisted.BonusLUK;
            tooltipPart.BonusHP = persisted.BonusHP;
            tooltipPart.BonusMP = persisted.BonusMP;
            tooltipPart.BonusWeaponAttack = persisted.BonusWeaponAttack;
            tooltipPart.BonusMagicAttack = persisted.BonusMagicAttack;
            tooltipPart.BonusWeaponDefense = persisted.BonusWeaponDefense;
            tooltipPart.BonusMagicDefense = persisted.BonusMagicDefense;
            tooltipPart.BonusAccuracy = persisted.BonusAccuracy;
            tooltipPart.BonusAvoidability = persisted.BonusAvoidability;
            tooltipPart.BonusHands = persisted.BonusHands;
            tooltipPart.BonusSpeed = persisted.BonusSpeed;
            tooltipPart.BonusJump = persisted.BonusJump;
            tooltipPart.UpgradeSlots = persisted.UpgradeSlots;
            tooltipPart.TotalUpgradeSlotCount = persisted.TotalUpgradeSlotCount;
            tooltipPart.RemainingUpgradeSlotCount = persisted.RemainingUpgradeSlotCount;
            tooltipPart.EnhancementStarCount = persisted.EnhancementStarCount;
            tooltipPart.KnockbackRate = persisted.KnockbackRate;
            tooltipPart.TradeAvailable = persisted.TradeAvailable;
            tooltipPart.IsTimeLimited = persisted.IsTimeLimited;
            tooltipPart.PotentialTierText = persisted.PotentialTierText;
            tooltipPart.PotentialLines = persisted.PotentialLines != null ? new List<string>(persisted.PotentialLines) : new List<string>();
            tooltipPart.ItemOptionIds = persisted.ItemOptionIds != null ? new List<int>(persisted.ItemOptionIds) : new List<int>();
            tooltipPart.HasGrowthInfo = persisted.HasGrowthInfo;
            tooltipPart.GrowthLevel = persisted.GrowthLevel;
            tooltipPart.GrowthMaxLevel = persisted.GrowthMaxLevel;
            tooltipPart.GrowthExpPercent = persisted.GrowthExpPercent;

            if (tooltipPart is WeaponPart weaponPart)
            {
                weaponPart.AttackSpeed = persisted.AttackSpeed;
                weaponPart.WeaponType = persisted.WeaponType;
            }

            return tooltipPart;
        }
    }
}
