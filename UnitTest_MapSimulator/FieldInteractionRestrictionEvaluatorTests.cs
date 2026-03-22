using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data;

namespace UnitTest_MapSimulator;

public sealed class FieldInteractionRestrictionEvaluatorTests
{
    [Fact]
    public void GetTeleportItemRestrictionMessage_ReturnsExplicitNotice_WhenFieldLimitBlocksTeleportItems()
    {
        long fieldLimit = 1L << (int)FieldLimitType.Unable_To_Use_Teleport_Item;

        string message = FieldInteractionRestrictionEvaluator.GetTeleportItemRestrictionMessage(fieldLimit);

        Assert.Equal("Teleport items cannot be used in this map.", message);
        Assert.Equal(message, FieldInteractionRestrictionEvaluator.GetMapTransferRestrictionMessage(fieldLimit));
    }

    [Fact]
    public void IsTeleportItem_RecognizesTeleportRockCashFamily()
    {
        Assert.True(TeleportItemUsageEvaluator.IsTeleportItem(
            5040000,
            InventoryType.CASH,
            "The Teleport Rock",
            "Remembers 5 maps of your choice and lets you teleport to the map you remembered."));

        Assert.True(TeleportItemUsageEvaluator.IsTeleportItem(
            5040004,
            InventoryType.CASH,
            "Hyper Teleport Rock",
            "Allows you to teleport to most other locations through the World Map [W]."));
    }

    [Fact]
    public void IsTeleportItem_RejectsUnrelatedItems()
    {
        Assert.False(TeleportItemUsageEvaluator.IsTeleportItem(
            2120000,
            InventoryType.CASH,
            "Pet Snack",
            "Tasty pet food."));

        Assert.False(TeleportItemUsageEvaluator.IsTeleportItem(
            5040000,
            InventoryType.USE,
            "The Teleport Rock",
            "Remembers 5 maps of your choice."));
    }
}
