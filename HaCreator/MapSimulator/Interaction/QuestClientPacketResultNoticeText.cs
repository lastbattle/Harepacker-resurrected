using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class QuestClientPacketResultNoticeText
    {
        // Recovered from CUserLocal::OnQuestResult subtype 12:
        // 3292 wraps the inventory-category summary, 3293 is the separator,
        // 4552 wraps the negative-meso variant, and category labels resolve
        // through StringPool ids 10, 6791, 11, and 6712.
        internal const int RewardInventorySummaryStringPoolId = 3292;
        internal const int RewardInventorySeparatorStringPoolId = 3293;
        internal const int RewardInventoryNegativeMesoStringPoolId = 4552;
        internal const int EquipInventoryCategoryStringPoolId = 10;
        internal const int UseInventoryCategoryStringPoolId = 6791;
        internal const int SetupInventoryCategoryStringPoolId = 11;
        internal const int EtcInventoryCategoryStringPoolId = 6712;

        private const string RewardInventorySeparatorFallback = ", ";
        private const string RewardInventorySummarySingleFallback = "Check your {0} inventory tab for quest rewards.";
        private const string RewardInventorySummaryMultipleFallback = "Check your {0} inventory tabs for quest rewards.";
        private const string RewardInventoryNegativeMesoFallback = "{0}\nThis quest also deducts mesos.";
        private const string NegativeMesoOnlyFallback = "This quest deducts mesos.";

        internal static bool TryResolveInventoryCategoryLabel(InventoryType inventoryType, out string label, out int stringPoolId)
        {
            switch (inventoryType)
            {
                case InventoryType.EQUIP:
                    label = "Equip";
                    stringPoolId = EquipInventoryCategoryStringPoolId;
                    return true;
                case InventoryType.USE:
                    label = "Use";
                    stringPoolId = UseInventoryCategoryStringPoolId;
                    return true;
                case InventoryType.SETUP:
                    label = "Setup";
                    stringPoolId = SetupInventoryCategoryStringPoolId;
                    return true;
                case InventoryType.ETC:
                    label = "Etc";
                    stringPoolId = EtcInventoryCategoryStringPoolId;
                    return true;
                default:
                    label = null;
                    stringPoolId = -1;
                    return false;
            }
        }

        internal static string DescribeRewardItemCategories(IEnumerable<int> itemIds)
        {
            if (itemIds == null)
            {
                return string.Empty;
            }

            var labels = new List<string>();
            var seenPoolIds = new HashSet<int>();
            foreach (int itemId in itemIds)
            {
                InventoryType? inventoryType = InventoryTypeExtensions.GetByType((byte)(Math.Max(0, itemId) / 1000000));
                if (!inventoryType.HasValue ||
                    !TryResolveInventoryCategoryLabel(inventoryType.Value, out string label, out int stringPoolId) ||
                    !seenPoolIds.Add(stringPoolId))
                {
                    continue;
                }

                labels.Add(label);
            }

            return string.Join(RewardInventorySeparatorFallback, labels);
        }

        internal static string FormatRewardInventoryNotice(IEnumerable<int> itemIds)
        {
            string categoryText = DescribeRewardItemCategories(itemIds);
            if (string.IsNullOrWhiteSpace(categoryText))
            {
                return string.Empty;
            }

            bool hasMultipleCategories = categoryText.Contains(RewardInventorySeparatorFallback, StringComparison.Ordinal);
            return string.Format(
                hasMultipleCategories
                    ? RewardInventorySummaryMultipleFallback
                    : RewardInventorySummarySingleFallback,
                categoryText);
        }

        internal static string ApplyNegativeMesoWrap(string summaryText)
        {
            return string.IsNullOrWhiteSpace(summaryText)
                ? NegativeMesoOnlyFallback
                : string.Format(RewardInventoryNegativeMesoFallback, summaryText);
        }
    }
}
