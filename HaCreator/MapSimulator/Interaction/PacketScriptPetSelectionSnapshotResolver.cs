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
        LiveCashInventory = 2,
        PacketPayloadFallback = 3
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

            List<PacketCharacterDataItemSlot> authoritativePetItems = authoritativeCashItems?
                .Where(static item =>
                    item.HasCashItemSerialNumber &&
                    item.CashItemSerialNumber > 0 &&
                    IsPetCashItem(item.ItemId))
                .ToList()
                ?? new List<PacketCharacterDataItemSlot>();
            HashSet<int> matchedLiveIndices = new();
            List<PacketCharacterDataItemSlot> unresolvedAuthoritativeItems = new();

            for (int i = 0; i < liveCashSlots.Count; i++)
            {
                InventorySlotData slot = liveCashSlots[i];
                if (slot == null)
                {
                    continue;
                }

                if (!IsPetCashItem(slot.ItemId))
                {
                    continue;
                }

                slot.CashItemSerialNumber = null;
            }

            foreach (PacketCharacterDataItemSlot authoritativeItem in authoritativePetItems)
            {
                int liveIndex = authoritativeItem.InventoryPosition - 1;
                if (liveIndex >= 0 &&
                    liveIndex < liveCashSlots.Count &&
                    !matchedLiveIndices.Contains(liveIndex))
                {
                    InventorySlotData liveSlot = liveCashSlots[liveIndex];
                    if (liveSlot != null &&
                        IsPetCashItem(liveSlot.ItemId) &&
                        liveSlot.ItemId == authoritativeItem.ItemId)
                    {
                        liveSlot.CashItemSerialNumber = authoritativeItem.CashItemSerialNumber;
                        matchedLiveIndices.Add(liveIndex);
                        continue;
                    }
                }

                unresolvedAuthoritativeItems.Add(authoritativeItem);
            }

            if (unresolvedAuthoritativeItems.Count == 0)
            {
                return;
            }

            Dictionary<int, List<int>> unresolvedLiveIndicesByItemId = BuildUnresolvedLiveIndicesByItemId(liveCashSlots, matchedLiveIndices);
            foreach (IGrouping<int, PacketCharacterDataItemSlot> group in unresolvedAuthoritativeItems.GroupBy(static item => item.ItemId))
            {
                if (!unresolvedLiveIndicesByItemId.TryGetValue(group.Key, out List<int> liveIndices) ||
                    liveIndices.Count == 0)
                {
                    continue;
                }

                PacketCharacterDataItemSlot[] authoritativeMatches = group.ToArray();
                if (TryApplyUniqueSameItemRemap(liveCashSlots, liveIndices, authoritativeMatches))
                {
                    continue;
                }

                TryApplyOrderedDuplicateSameItemRemap(liveCashSlots, liveIndices, authoritativeMatches);
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

        internal static PacketScriptMessageRuntime.PacketScriptPetSelectionCandidate ResolveLiveInventoryCandidateBySlotHint(
            IReadOnlyList<InventorySlotData> liveCashSlots,
            long petSerialNumber,
            byte packetSlotHint)
        {
            if (liveCashSlots == null || petSerialNumber <= 0 || packetSlotHint <= 0)
            {
                return null;
            }

            int liveIndex = packetSlotHint - 1;
            if (liveIndex < 0 || liveIndex >= liveCashSlots.Count)
            {
                return null;
            }

            InventorySlotData slot = liveCashSlots[liveIndex];
            if (slot == null || !IsPetCashItem(slot.ItemId))
            {
                return null;
            }

            string displayName = ResolveLiveInventoryDisplayName(slot.ItemId, slot.ItemName, packetSlotHint);
            return new PacketScriptMessageRuntime.PacketScriptPetSelectionCandidate(
                petSerialNumber,
                liveIndex,
                slot.ItemId,
                displayName,
                PacketScriptPetSelectionSource.LiveCashInventory,
                packetSlotHint);
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

        private static Dictionary<int, List<int>> BuildUnresolvedLiveIndicesByItemId(
            IReadOnlyList<InventorySlotData> liveCashSlots,
            HashSet<int> matchedLiveIndices)
        {
            Dictionary<int, List<int>> unresolved = new();
            for (int i = 0; i < liveCashSlots.Count; i++)
            {
                if (matchedLiveIndices.Contains(i))
                {
                    continue;
                }

                InventorySlotData slot = liveCashSlots[i];
                if (slot == null || !IsPetCashItem(slot.ItemId))
                {
                    continue;
                }

                if (!unresolved.TryGetValue(slot.ItemId, out List<int> liveIndices))
                {
                    liveIndices = new List<int>();
                    unresolved[slot.ItemId] = liveIndices;
                }

                liveIndices.Add(i);
            }

            return unresolved;
        }

        private static bool TryApplyUniqueSameItemRemap(
            IReadOnlyList<InventorySlotData> liveCashSlots,
            IReadOnlyList<int> liveIndices,
            IReadOnlyList<PacketCharacterDataItemSlot> authoritativeMatches)
        {
            if (liveIndices == null ||
                authoritativeMatches == null ||
                liveIndices.Count != 1 ||
                authoritativeMatches.Count != 1)
            {
                return false;
            }

            int liveIndex = liveIndices[0];
            liveCashSlots[liveIndex].CashItemSerialNumber = authoritativeMatches[0].CashItemSerialNumber;
            return true;
        }

        private static bool TryApplyOrderedDuplicateSameItemRemap(
            IReadOnlyList<InventorySlotData> liveCashSlots,
            IReadOnlyList<int> liveIndices,
            IReadOnlyList<PacketCharacterDataItemSlot> authoritativeMatches)
        {
            if (liveIndices == null ||
                authoritativeMatches == null ||
                liveIndices.Count <= 1 ||
                liveIndices.Count != authoritativeMatches.Count)
            {
                return false;
            }

            PacketCharacterDataItemSlot[] orderedAuthoritative = authoritativeMatches
                .OrderBy(static item => item.InventoryPosition)
                .ToArray();
            int[] orderedLiveIndices = liveIndices
                .OrderBy(static index => index)
                .ToArray();

            for (int i = 0; i < orderedLiveIndices.Length; i++)
            {
                int liveIndex = orderedLiveIndices[i];
                liveCashSlots[liveIndex].CashItemSerialNumber = orderedAuthoritative[i].CashItemSerialNumber;
            }

            return true;
        }
    }
}
