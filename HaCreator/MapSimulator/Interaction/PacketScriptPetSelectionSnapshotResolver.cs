using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum PacketScriptPetSelectionSource
    {
        ActivePetRuntime = 0,
        AuthoritativeCharacterData = 1,
        LiveCashInventory = 2
    }

    internal static class PacketScriptPetSelectionSnapshotResolver
    {
        internal static IReadOnlyDictionary<long, PacketScriptMessageRuntime.PacketScriptPetSelectionCandidate> BuildCandidates(PacketCharacterDataSnapshot snapshot)
        {
            Dictionary<long, PacketScriptMessageRuntime.PacketScriptPetSelectionCandidate> candidates = new();
            if (snapshot?.InventoryItemsByType == null ||
                !snapshot.InventoryItemsByType.TryGetValue(InventoryType.CASH, out IReadOnlyList<PacketCharacterDataItemSlot> cashItems) ||
                cashItems == null)
            {
                return candidates;
            }

            foreach (PacketCharacterDataItemSlot cashItem in cashItems)
            {
                if (!cashItem.HasCashItemSerialNumber ||
                    cashItem.CashItemSerialNumber <= 0 ||
                    !IsPetCashItem(cashItem.ItemId) ||
                    candidates.ContainsKey(cashItem.CashItemSerialNumber))
                {
                    continue;
                }

                string displayName = ResolvePetDisplayName(cashItem.ItemId, cashItem.InventoryPosition);
                candidates[cashItem.CashItemSerialNumber] = new PacketScriptMessageRuntime.PacketScriptPetSelectionCandidate(
                    cashItem.CashItemSerialNumber,
                    SlotIndex: -1,
                    cashItem.ItemId,
                    displayName,
                    PacketScriptPetSelectionSource.AuthoritativeCharacterData,
                    cashItem.InventoryPosition);
            }

            return candidates;
        }

        internal static void ApplyAuthoritativeCashSerialMetadata(
            IReadOnlyList<InventorySlotData> liveCashSlots,
            IReadOnlyList<PacketCharacterDataItemSlot> authoritativeCashItems)
        {
            if (liveCashSlots == null)
            {
                return;
            }

            Dictionary<short, PacketCharacterDataItemSlot> itemsByPosition = authoritativeCashItems?
                .GroupBy(static item => item.InventoryPosition)
                .Select(static group => group.First())
                .ToDictionary(static item => item.InventoryPosition)
                ?? new Dictionary<short, PacketCharacterDataItemSlot>();

            for (int i = 0; i < liveCashSlots.Count; i++)
            {
                InventorySlotData slot = liveCashSlots[i];
                if (slot == null || !IsPetCashItem(slot.ItemId))
                {
                    continue;
                }

                short inventoryPosition = (short)(i + 1);
                if (itemsByPosition.TryGetValue(inventoryPosition, out PacketCharacterDataItemSlot authoritativeItem)
                    && authoritativeItem.ItemId == slot.ItemId
                    && authoritativeItem.HasCashItemSerialNumber
                    && authoritativeItem.CashItemSerialNumber > 0)
                {
                    slot.CashItemSerialNumber = authoritativeItem.CashItemSerialNumber;
                }
                else
                {
                    slot.CashItemSerialNumber = null;
                }
            }
        }

        internal static PacketScriptMessageRuntime.PacketScriptPetSelectionCandidate ResolveLiveInventoryCandidate(
            IReadOnlyList<InventorySlotData> liveCashSlots,
            long petSerialNumber)
        {
            if (liveCashSlots == null || petSerialNumber <= 0)
            {
                return null;
            }

            for (int i = 0; i < liveCashSlots.Count; i++)
            {
                InventorySlotData slot = liveCashSlots[i];
                if (slot == null
                    || slot.CashItemSerialNumber.GetValueOrDefault() != petSerialNumber
                    || !IsPetCashItem(slot.ItemId))
                {
                    continue;
                }

                string displayName = ResolveLiveInventoryDisplayName(slot.ItemId, slot.ItemName, (short)(i + 1));
                return new PacketScriptMessageRuntime.PacketScriptPetSelectionCandidate(
                    petSerialNumber,
                    i,
                    slot.ItemId,
                    displayName,
                    PacketScriptPetSelectionSource.LiveCashInventory,
                    (short)(i + 1));
            }

            return null;
        }

        private static bool IsPetCashItem(int itemId)
        {
            return itemId > 0 && itemId / 10000 == 500;
        }

        private static string ResolvePetDisplayName(int itemId, short inventoryPosition)
        {
            if (InventoryItemMetadataResolver.TryResolveItemName(itemId, out string itemName) &&
                !string.IsNullOrWhiteSpace(itemName))
            {
                return itemName.Trim();
            }

            return inventoryPosition > 0
                ? $"Pet in cash slot {inventoryPosition}"
                : $"Pet {itemId}";
        }

        private static string ResolveLiveInventoryDisplayName(int itemId, string itemName, short inventoryPosition)
        {
            if (!string.IsNullOrWhiteSpace(itemName))
            {
                return itemName.Trim();
            }

            return ResolvePetDisplayName(itemId, inventoryPosition);
        }
    }
}
