using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    internal static class AdminShopPacketOwnedSetUserItemsParity
    {
        internal static InventoryType ResolveSetUserItemsInventoryType(
            bool requiresInventorySource,
            InventoryType sourceInventoryType,
            int rewardItemId,
            int displayItemId)
        {
            if (requiresInventorySource && sourceInventoryType != InventoryType.NONE)
            {
                return sourceInventoryType;
            }

            int itemId = rewardItemId > 0 ? rewardItemId : displayItemId;
            return ResolveClientInventoryTypeFromItemId(itemId);
        }

        internal static int FindMatchingEntryIndex(
            IReadOnlyList<AdminShopUserSellMutationRow> currentRows,
            InventoryType inventoryType,
            int itemId,
            int slotPosition,
            int packetSerialNumber)
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
                    && currentRow.SlotPosition == slotPosition
                    && (packetSerialNumber <= 0 || currentRow.PacketSerialNumber == packetSerialNumber))
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
            int packetSerialNumber,
            int currentScrollOffset,
            int maxVisibleRows)
        {
            int selectedIndex = FindMatchingEntryIndex(currentRows, inventoryType, itemId, slotPosition, packetSerialNumber);
            int scrollOffset = AdminShopUserSellMutationParity.ComputeScrollOffset(
                currentScrollOffset,
                selectedIndex,
                maxVisibleRows);
            return new AdminShopUserSellMutationResolution(selectedIndex, scrollOffset);
        }

        private static InventoryType ResolveClientInventoryTypeFromItemId(int itemId)
        {
            if (itemId <= 0)
            {
                return InventoryType.NONE;
            }

            // CAdminShopDlg::SendTradeRequest forwards nItemID / 1000000 into SetUserItems.
            return (itemId / 1000000) switch
            {
                1 => InventoryType.EQUIP,
                2 => InventoryType.USE,
                3 => InventoryType.SETUP,
                4 => InventoryType.ETC,
                5 => InventoryType.CASH,
                _ => InventoryType.NONE
            };
        }
    }
}
