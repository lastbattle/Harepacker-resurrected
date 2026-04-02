using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class QuestClientPacketResultNoticeText
    {
        // Recovered from MapleStory.exe StringPool::ms_aString together with
        // CUserLocal::OnQuestResult subtype 10/12:
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
        internal const int QuestExpiredStringPoolId = 0x1015;

        private const string RewardInventorySeparatorText = " or";
        private const string RewardInventorySummaryText = "{0} item inventory is full.";
        private const string RewardInventoryNegativeMesoText = "Either you don't have enough Mesos or {0}";
        private const string QuestExpiredNoticeText = "The [{0}] quest expired because the time limit ended";

        internal static bool TryResolveInventoryCategoryLabel(InventoryType inventoryType, out string label, out int stringPoolId)
        {
            switch (inventoryType)
            {
                case InventoryType.EQUIP:
                    label = "Eqp";
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

            return string.Join(RewardInventorySeparatorText, labels);
        }

        internal static string FormatRewardInventoryNotice(IEnumerable<int> itemIds)
        {
            string categoryText = DescribeRewardItemCategories(itemIds);
            return string.Format(RewardInventorySummaryText, categoryText);
        }

        internal static string ApplyNegativeMesoWrap(string summaryText)
        {
            return string.Format(RewardInventoryNegativeMesoText, summaryText ?? string.Empty);
        }

        internal static string FormatQuestExpiredNotice(string questName)
        {
            return string.Format(
                QuestExpiredNoticeText,
                string.IsNullOrWhiteSpace(questName) ? "Unknown" : questName);
        }
    }
}
