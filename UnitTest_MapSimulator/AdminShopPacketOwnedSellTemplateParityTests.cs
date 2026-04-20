using HaCreator.MapSimulator.UI;

namespace UnitTest_MapSimulator;

public sealed class AdminShopPacketOwnedSellTemplateParityTests
{
    [Fact]
    public void IsClientSetUserItemsSlotEligible_RejectsRecoveredClientDisqualifiers()
    {
        Assert.False(AdminShopPacketOwnedSellTemplateParity.IsClientSetUserItemsSlotEligible(
            isCashItem: true,
            isNotForSale: false,
            isQuestItem: false,
            isCashOwnershipLocked: false,
            cashItemSerialNumber: 0));
        Assert.False(AdminShopPacketOwnedSellTemplateParity.IsClientSetUserItemsSlotEligible(
            isCashItem: false,
            isNotForSale: true,
            isQuestItem: false,
            isCashOwnershipLocked: false,
            cashItemSerialNumber: 0));
        Assert.False(AdminShopPacketOwnedSellTemplateParity.IsClientSetUserItemsSlotEligible(
            isCashItem: false,
            isNotForSale: false,
            isQuestItem: true,
            isCashOwnershipLocked: false,
            cashItemSerialNumber: 0));
        Assert.False(AdminShopPacketOwnedSellTemplateParity.IsClientSetUserItemsSlotEligible(
            isCashItem: false,
            isNotForSale: false,
            isQuestItem: false,
            isCashOwnershipLocked: true,
            cashItemSerialNumber: 0));
        Assert.False(AdminShopPacketOwnedSellTemplateParity.IsClientSetUserItemsSlotEligible(
            isCashItem: false,
            isNotForSale: false,
            isQuestItem: false,
            isCashOwnershipLocked: false,
            cashItemSerialNumber: 42));
    }

    [Fact]
    public void ResolveSourceRequestCountCap_UsesSlotStackAndConfiguredCap()
    {
        Assert.Equal(3, AdminShopPacketOwnedSellTemplateParity.ResolveSourceRequestCountCap(
            honorConfiguredMaxRequestCount: false,
            configuredMaxRequestCount: 1,
            sourceItemQuantity: 10,
            selectedSlotQuantity: 39));

        Assert.Equal(2, AdminShopPacketOwnedSellTemplateParity.ResolveSourceRequestCountCap(
            honorConfiguredMaxRequestCount: true,
            configuredMaxRequestCount: 2,
            sourceItemQuantity: 10,
            selectedSlotQuantity: 39));
    }

    [Theory]
    [InlineData(true, 0, false)]
    [InlineData(true, 1, true)]
    [InlineData(false, 0, true)]
    public void CanBuildSendTradeRequestPosition_MatchesInventorySourceGate(bool requiresInventorySource, int position, bool expected)
    {
        bool actual = AdminShopPacketOwnedSellTemplateParity.CanBuildSendTradeRequestPosition(requiresInventorySource, position);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SelectBestPacketOwnedCommodityMetadataSerial_PrefersExactSerialCandidate()
    {
        PacketOwnedCommodityMetadataCandidate[] candidates =
        [
            new PacketOwnedCommodityMetadataCandidate(110, 2000001, 100, true, 1, 0, 1),
            new PacketOwnedCommodityMetadataCandidate(111, 2000001, 100, true, 1, 0, 2)
        ];

        int selectedSerial = AdminShopPacketOwnedSellTemplateParity.SelectBestPacketOwnedCommodityMetadataSerial(
            packetSerialNumber: 110,
            packetItemId: 2000001,
            packetPrice: 100,
            packetMaxPerSlot: 1,
            expectedOnSale: true,
            candidates);

        Assert.Equal(110, selectedSerial);
    }
}
