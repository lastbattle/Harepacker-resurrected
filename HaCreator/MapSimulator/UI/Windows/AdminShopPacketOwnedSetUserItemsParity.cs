using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    internal static class AdminShopPacketOwnedSetUserItemsParity
    {
        internal static int FindMatchingEntryIndex(
            IReadOnlyList<AdminShopUserSellMutationRow> currentRows,
            InventoryType inventoryType,
            int itemId,
            int slotPosition)
        {
            if (currentRows == null || inventoryType == InventoryType.NONE || itemId <= 0 || slotPosition <= 0)
            {
                return -1;
            }

            for (int index = 0; index < currentRows.Count; index++)
            {
                AdminShopUserSellMutationRow currentRow = currentRows[index];
                if (currentRow.InventoryType == inventoryType
                    && currentRow.ItemId == itemId
                    && currentRow.SlotPosition == slotPosition)
                {
                    return index;
                }
            }

            return -1;
        }

        internal static AdminShopUserSellMutationResolution Resolve(
            IReadOnlyList<AdminShopUserSellMutationRow> currentRows,
            InventoryType inventoryType,
            int itemId,
            int slotPosition,
            int currentScrollOffset,
            int maxVisibleRows)
        {
            int selectedIndex = FindMatchingEntryIndex(currentRows, inventoryType, itemId, slotPosition);
            int scrollOffset = AdminShopUserSellMutationParity.ComputeScrollOffset(
                currentScrollOffset,
                selectedIndex,
                maxVisibleRows);
            return new AdminShopUserSellMutationResolution(selectedIndex, scrollOffset);
        }
    }
}
