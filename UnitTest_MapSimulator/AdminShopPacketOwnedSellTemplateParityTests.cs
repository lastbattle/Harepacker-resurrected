using HaCreator.MapSimulator.UI;

namespace UnitTest_MapSimulator
{
    public class AdminShopPacketOwnedSellTemplateParityTests
    {
        [Fact]
        public void NonPositivePacketPrice_RemainsInSellTemplateLane()
        {
            bool canCreateSellTemplateFallback = AdminShopPacketOwnedSellTemplateParity.CanCreateFallbackPacketOwnedSellTemplateRow(
                packetItemId: 0,
                packetPrice: 0);
            bool canCreateCommodityFallback = AdminShopPacketOwnedSellTemplateParity.CanCreateFallbackPacketOwnedCommodityRow(
                packetItemId: 0,
                packetPrice: 0);

            Assert.True(canCreateSellTemplateFallback);
            Assert.False(canCreateCommodityFallback);
        }

        [Fact]
        public void SetUserItemsSlotEligibility_RejectsCashAndLockedRows()
        {
            bool cashItemRejected = AdminShopPacketOwnedSellTemplateParity.IsClientSetUserItemsSlotEligible(
                isCashItem: true,
                isNotForSale: false,
                isQuestItem: false,
                isCashOwnershipLocked: false,
                cashItemSerialNumber: 0);
            bool lockedOwnershipRejected = AdminShopPacketOwnedSellTemplateParity.IsClientSetUserItemsSlotEligible(
                isCashItem: false,
                isNotForSale: false,
                isQuestItem: false,
                isCashOwnershipLocked: true,
                cashItemSerialNumber: 0);
            bool normalSlotAccepted = AdminShopPacketOwnedSellTemplateParity.IsClientSetUserItemsSlotEligible(
                isCashItem: false,
                isNotForSale: false,
                isQuestItem: false,
                isCashOwnershipLocked: false,
                cashItemSerialNumber: 0);

            Assert.False(cashItemRejected);
            Assert.False(lockedOwnershipRejected);
            Assert.True(normalSlotAccepted);
        }

        [Fact]
        public void ResolveSourceRequestCountCap_UsesStackCount_WhenPacketOwnedSnapshotSkipsConfiguredCap()
        {
            int requestCountCap = AdminShopPacketOwnedSellTemplateParity.ResolveSourceRequestCountCap(
                honorConfiguredMaxRequestCount: false,
                configuredMaxRequestCount: 999,
                sourceItemQuantity: 3,
                selectedSlotQuantity: 7);

            Assert.Equal(2, requestCountCap);
        }
    }
}
