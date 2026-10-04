using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;

namespace HaCreator.MapSimulator.Fields
{
    /// <summary>
    /// Identifies cash teleport items that should route into the map-transfer UI.
    /// WZ evidence: string/Cash.img/5040000 and 5040004 describe Teleport Rock behavior.
    /// </summary>
    public static class TeleportItemUsageEvaluator
    {
        private const int TeleportCashGroup = 504;

        public static bool IsTeleportItem(int itemId, InventoryType inventoryType, string itemName, string itemDescription)
        {
            if (itemId <= 0 || inventoryType != InventoryType.CASH)
            {
                return false;
            }

            if ((itemId / 10000) != TeleportCashGroup)
            {
                return false;
            }

            if (ContainsTeleportCue(itemName) || ContainsTeleportCue(itemDescription))
            {
                return true;
            }

            // 504xxxx cash items live in the Teleport Rock family in Cash/0504.img.
            return true;
        }

        private static bool ContainsTeleportCue(string text)
        {
            return !string.IsNullOrWhiteSpace(text)
                   && text.IndexOf("teleport", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
