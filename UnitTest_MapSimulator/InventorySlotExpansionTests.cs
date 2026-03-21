using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;

namespace UnitTest_MapSimulator;

public sealed class InventorySlotExpansionTests
{
    [Fact]
    public void InventoryStartsAtClientDefaultSlotLimit()
    {
        InventoryUI inventory = new(null, null, null);

        Assert.Equal(24, inventory.GetSlotLimit(InventoryType.EQUIP));
        Assert.Equal(24, inventory.GetSlotLimit(InventoryType.USE));
        Assert.Equal(24, inventory.GetSlotLimit(InventoryType.SETUP));
        Assert.Equal(24, inventory.GetSlotLimit(InventoryType.ETC));
        Assert.Equal(24, inventory.GetSlotLimit(InventoryType.CASH));
    }

    [Fact]
    public void ExpandingSlotsAdvancesByFourUntilCap()
    {
        InventoryUI inventory = new(null, null, null);

        Assert.True(inventory.TryExpandSlotLimit(InventoryType.EQUIP));
        Assert.Equal(28, inventory.GetSlotLimit(InventoryType.EQUIP));

        while (inventory.TryExpandSlotLimit(InventoryType.EQUIP))
        {
        }

        Assert.Equal(96, inventory.GetSlotLimit(InventoryType.EQUIP));
        Assert.False(inventory.CanExpandSlotLimit(InventoryType.EQUIP));
        Assert.False(inventory.TryExpandSlotLimit(InventoryType.EQUIP));
    }

    [Fact]
    public void ExpansionAmountIsRoundedUpToClientRowSize()
    {
        InventoryUI inventory = new(null, null, null);

        Assert.True(inventory.TryExpandSlotLimit(InventoryType.USE, 1));
        Assert.Equal(28, inventory.GetSlotLimit(InventoryType.USE));

        Assert.True(inventory.TryExpandSlotLimit(InventoryType.USE, 6));
        Assert.Equal(36, inventory.GetSlotLimit(InventoryType.USE));
    }
}
