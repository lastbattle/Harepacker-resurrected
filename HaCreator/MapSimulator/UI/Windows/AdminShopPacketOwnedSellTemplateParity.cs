using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    internal static class AdminShopPacketOwnedSellTemplateParity
    {
        internal static bool IsClientSetUserItemsSlotEligible(
            bool isCashItem,
            bool isNotForSale,
            bool isQuestItem,
            long? cashItemSerialNumber)
        {
            return !isCashItem
                && !isNotForSale
                && !isQuestItem
                && cashItemSerialNumber.GetValueOrDefault() == 0L;
        }

        internal static T SelectFirstMatchingTemplate<T>(IReadOnlyList<T> templates)
        {
            return templates == null || templates.Count == 0
                ? default
                : templates[0];
        }
    }
}
