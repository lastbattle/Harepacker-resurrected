using HaCreator.MapSimulator.UI;
using Xunit;

namespace UnitTest_MapSimulator;

public sealed class AdminShopPacketOwnedCommodityFallbackParityTests
{
    [Fact]
    public void CanCreateFallbackPacketOwnedCommodityRow_AllowsSeriallessOpaqueBuyRows()
    {
        bool canCreate = AdminShopPacketOwnedSellTemplateParity.CanCreateFallbackPacketOwnedCommodityRow(
            packetItemId: 0,
            packetPrice: 1500);

        Assert.True(canCreate);
    }

    [Fact]
    public void CanCreateFallbackPacketOwnedSellTemplateRow_AllowsSeriallessOpaqueSellRows()
    {
        bool canCreate = AdminShopPacketOwnedSellTemplateParity.CanCreateFallbackPacketOwnedSellTemplateRow(
            packetItemId: 0,
            packetPrice: -1);

        Assert.True(canCreate);
    }

    [Fact]
    public void CanCreateFallbackPacketOwnedCommodityRow_RejectsResolvedItemRows()
    {
        bool canCreate = AdminShopPacketOwnedSellTemplateParity.CanCreateFallbackPacketOwnedCommodityRow(
            packetItemId: 2000005,
            packetPrice: 1500);

        Assert.False(canCreate);
    }

    [Fact]
    public void CanCreateFallbackPacketOwnedSellTemplateRow_RejectsPositivePriceRows()
    {
        bool canCreate = AdminShopPacketOwnedSellTemplateParity.CanCreateFallbackPacketOwnedSellTemplateRow(
            packetItemId: 0,
            packetPrice: 1);

        Assert.False(canCreate);
    }
}
