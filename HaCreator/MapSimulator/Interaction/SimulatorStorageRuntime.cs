using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class SimulatorStorageRuntime : IStorageRuntime
    {
        private const int DefaultSlotLimit = 24;
        private const int MinSlotLimit = 24;
        private const int MaxSlotLimit = 96;
        private const int SlotExpansionStep = 4;

        private readonly Dictionary<InventoryType, List<InventorySlotData>> _storageItems = new()
        {
            { InventoryType.EQUIP, new List<InventorySlotData>() },
            { InventoryType.USE, new List<InventorySlotData>() },
            { InventoryType.SETUP, new List<InventorySlotData>() },
            { InventoryType.ETC, new List<InventorySlotData>() },
            { InventoryType.CASH, new List<InventorySlotData>() }
        };
        private readonly List<string> _sharedCharacterNames = new();
        private readonly HashSet<string> _sharedCharacterLookup = new(StringComparer.OrdinalIgnoreCase);

        private int _slotLimit = DefaultSlotLimit;
        private long _meso;

        public string AccountLabel { get; private set; } = "Simulator Account Storage";
        public string CurrentCharacterName { get; private set; } = string.Empty;
        public IReadOnlyList<string> SharedCharacterNames => _sharedCharacterNames;

        public bool CanCurrentCharacterAccess =>
            _sharedCharacterNames.Count == 0 ||
            string.IsNullOrWhiteSpace(CurrentCharacterName) ||
            _sharedCharacterLookup.Contains(CurrentCharacterName);

        public IReadOnlyList<InventorySlotData> GetSlots(InventoryType type)
        {
            return _storageItems.TryGetValue(type, out List<InventorySlotData> rows)
                ? rows
                : Array.Empty<InventorySlotData>();
        }

        public int GetSlotLimit()
        {
            return _slotLimit;
        }

        public void SetSlotLimit(int slotLimit)
        {
            _slotLimit = Math.Clamp(slotLimit, MinSlotLimit, MaxSlotLimit);
        }

        public int GetUsedSlotCount()
        {
            int total = 0;
            foreach (List<InventorySlotData> rows in _storageItems.Values)
            {
                total += rows.Count;
            }

            return total;
        }

        public bool CanExpandSlotLimit(int amount = SlotExpansionStep)
        {
            int normalizedAmount = NormalizeSlotExpansionAmount(amount);
            return _slotLimit + normalizedAmount <= MaxSlotLimit;
        }

        public bool TryExpandSlotLimit(int amount = SlotExpansionStep)
        {
            if (!CanExpandSlotLimit(amount))
            {
                return false;
            }

            _slotLimit += NormalizeSlotExpansionAmount(amount);
            return true;
        }

        public long GetMesoCount()
        {
            return _meso;
        }

        public void SetMeso(long amount)
        {
            _meso = Math.Max(0, amount);
        }

        public void AddItem(InventoryType type, InventorySlotData slotData)
        {
            if (type == InventoryType.NONE || slotData == null || !_storageItems.TryGetValue(type, out List<InventorySlotData> rows))
            {
                return;
            }

            InventorySlotData source = slotData.Clone();
            int maxStackSize = InventoryItemMetadataResolver.ResolveMaxStack(type, source.MaxStackSize);
            int remainingQuantity = Math.Max(1, source.Quantity);
            if (IsStackable(type, maxStackSize))
            {
                remainingQuantity = FillExistingStacks(rows, source, remainingQuantity, maxStackSize);
            }

            while (remainingQuantity > 0 && GetUsedSlotCount() < _slotLimit)
            {
                InventorySlotData stack = source.Clone();
                stack.Quantity = IsStackable(type, maxStackSize)
                    ? Math.Min(remainingQuantity, maxStackSize)
                    : 1;
                stack.MaxStackSize = maxStackSize;
                rows.Add(stack);
                remainingQuantity -= stack.Quantity;
            }
        }

        public bool CanAcceptItem(InventoryType type, InventorySlotData slotData)
        {
            if (slotData == null || !_storageItems.TryGetValue(type, out List<InventorySlotData> rows))
            {
                return false;
            }

            int remainingQuantity = Math.Max(1, slotData.Quantity);
            int maxStackSize = InventoryItemMetadataResolver.ResolveMaxStack(type, slotData.MaxStackSize);
            if (IsStackable(type, maxStackSize))
            {
                foreach (InventorySlotData row in rows)
                {
                    if (row == null || row.ItemId != slotData.ItemId || row.IsDisabled)
                    {
                        continue;
                    }

                    int rowMax = InventoryItemMetadataResolver.ResolveMaxStack(type, row.MaxStackSize);
                    int capacity = rowMax - Math.Max(1, row.Quantity);
                    if (capacity <= 0)
                    {
                        continue;
                    }

                    remainingQuantity -= capacity;
                    if (remainingQuantity <= 0)
                    {
                        return true;
                    }
                }
            }

            int freeSlots = Math.Max(0, _slotLimit - GetUsedSlotCount());
            if (!IsStackable(type, maxStackSize))
            {
                return freeSlots >= remainingQuantity;
            }

            int neededStacks = (remainingQuantity + maxStackSize - 1) / maxStackSize;
            return freeSlots >= neededStacks;
        }

        public bool TryRemoveSlotAt(InventoryType type, int slotIndex, out InventorySlotData slotData)
        {
            slotData = null;
            if (!_storageItems.TryGetValue(type, out List<InventorySlotData> rows) ||
                slotIndex < 0 ||
                slotIndex >= rows.Count)
            {
                return false;
            }

            slotData = rows[slotIndex]?.Clone();
            rows.RemoveAt(slotIndex);
            return slotData != null;
        }

        public void SortSlots(InventoryType type)
        {
            if (_storageItems.TryGetValue(type, out List<InventorySlotData> rows))
            {
                rows.Sort(CompareSlots);
            }
        }

        public void ConfigureAccess(string accountLabel, string currentCharacterName, IEnumerable<string> sharedCharacterNames)
        {
            AccountLabel = string.IsNullOrWhiteSpace(accountLabel) ? "Simulator Account Storage" : accountLabel;
            CurrentCharacterName = currentCharacterName ?? string.Empty;

            _sharedCharacterNames.Clear();
            _sharedCharacterLookup.Clear();
            foreach (string characterName in sharedCharacterNames ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(characterName) || !_sharedCharacterLookup.Add(characterName))
                {
                    continue;
                }

                _sharedCharacterNames.Add(characterName);
            }
        }

        private static int NormalizeSlotExpansionAmount(int amount)
        {
            if (amount <= 0)
            {
                return SlotExpansionStep;
            }

            int remainder = amount % SlotExpansionStep;
            return remainder == 0 ? amount : amount + (SlotExpansionStep - remainder);
        }

        private static bool IsStackable(InventoryType type, int maxStackSize)
        {
            return type != InventoryType.EQUIP && maxStackSize > 1;
        }

        private static int FillExistingStacks(List<InventorySlotData> rows, InventorySlotData slotData, int remainingQuantity, int maxStackSize)
        {
            for (int i = 0; i < rows.Count && remainingQuantity > 0; i++)
            {
                InventorySlotData existing = rows[i];
                if (existing == null || existing.ItemId != slotData.ItemId || existing.IsDisabled)
                {
                    continue;
                }

                int rowMax = InventoryItemMetadataResolver.ResolveMaxStack(
                    InventoryItemMetadataResolver.ResolveInventoryType(existing.ItemId),
                    existing.MaxStackSize);
                int capacity = rowMax - Math.Max(1, existing.Quantity);
                if (capacity <= 0)
                {
                    continue;
                }

                int quantityToMerge = Math.Min(capacity, remainingQuantity);
                existing.Quantity += quantityToMerge;
                existing.MaxStackSize = maxStackSize;
                existing.ItemTexture ??= slotData.ItemTexture;
                existing.ItemName ??= slotData.ItemName;
                existing.ItemTypeName ??= slotData.ItemTypeName;
                existing.Description ??= slotData.Description;
                existing.GradeFrameIndex ??= slotData.GradeFrameIndex;
                existing.IsActiveBullet |= slotData.IsActiveBullet;
                remainingQuantity -= quantityToMerge;
            }

            return remainingQuantity;
        }

        private static int CompareSlots(InventorySlotData left, InventorySlotData right)
        {
            int leftId = left?.ItemId ?? int.MaxValue;
            int rightId = right?.ItemId ?? int.MaxValue;
            int idComparison = leftId.CompareTo(rightId);
            if (idComparison != 0)
            {
                return idComparison;
            }

            int rightQuantity = right?.Quantity ?? 0;
            int leftQuantity = left?.Quantity ?? 0;
            return rightQuantity.CompareTo(leftQuantity);
        }
    }
}
