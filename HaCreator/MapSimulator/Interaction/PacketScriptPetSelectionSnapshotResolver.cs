using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum PacketScriptPetSelectionSource
    {
        ActivePetRuntime = 0,
        AuthoritativeCharacterData = 1
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
    }
}
