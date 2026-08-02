using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    internal readonly record struct AdminShopUserSellMutationRow(
        InventoryType InventoryType,
        int ItemId,
        int SlotPosition,
        int Stock,
        int PacketSerialNumber);

    internal readonly record struct AdminShopUserSellMutationResolution(
        int SelectedIndex,
        int ScrollOffset);

    internal static class AdminShopUserSellMutationParity
    {
        internal static int FindMatchingEntryIndex(
            IReadOnlyList<AdminShopUserSellMutationRow> currentRows,
            AdminShopUserSellMutationRow requestedRow)
        {
            if (currentRows == null)
            {
                return -1;
            }

            for (int index = 0; index < currentRows.Count; index++)
            {
                AdminShopUserSellMutationRow currentRow = currentRows[index];
                if (currentRow.InventoryType == requestedRow.InventoryType
                    && currentRow.ItemId == requestedRow.ItemId
                    && currentRow.SlotPosition == requestedRow.SlotPosition)
                {
                    return index;
                }
            }

            return -1;
        }

        internal static int ComputeCmpSellItemIndex(
            IReadOnlyList<AdminShopUserSellMutationRow> snapshotRows,
            IReadOnlyList<AdminShopUserSellMutationRow> currentRows)
        {
            int snapshotCount = snapshotRows?.Count ?? 0;
            int currentCount = currentRows?.Count ?? 0;
            int countDiff = currentCount - snapshotCount;
            int comparedCount = Math.Min(snapshotCount, currentCount);
            int index = 0;

            while (index < comparedCount)
            {
                if (!snapshotRows[index].Equals(currentRows[index]))
                {
                    return index;
                }

                index++;
            }

            if (countDiff == 0)
            {
                return 0;
            }

            return countDiff > 0
                ? index
                : index - 1;
        }

        internal static int ComputeScrollOffset(int currentScrollOffset, int selectedIndex, int maxVisibleRows)
        {
            int normalizedVisibleRows = Math.Max(1, maxVisibleRows);
            if (selectedIndex < 0)
            {
                return Math.Max(0, currentScrollOffset);
            }

            int scrollOffset = Math.Max(0, currentScrollOffset);
            if (selectedIndex < scrollOffset)
            {
                return selectedIndex;
            }

            int lowerVisibleBound = scrollOffset + normalizedVisibleRows - 1;
            if (selectedIndex > lowerVisibleBound)
            {
                return Math.Max(0, selectedIndex - (normalizedVisibleRows - 1));
            }

            return scrollOffset;
        }

        internal static AdminShopUserSellMutationResolution Resolve(
            IReadOnlyList<AdminShopUserSellMutationRow> snapshotRows,
            IReadOnlyList<AdminShopUserSellMutationRow> currentRows,
            int currentScrollOffset,
            int maxVisibleRows)
        {
            int selectedIndex = ComputeCmpSellItemIndex(snapshotRows, currentRows);
            int scrollOffset = ComputeScrollOffset(currentScrollOffset, selectedIndex, maxVisibleRows);
            return new AdminShopUserSellMutationResolution(selectedIndex, scrollOffset);
        }
    }
}
