using HaCreator.MapSimulator;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;

namespace UnitTest_MapSimulator;

public sealed class FieldRuleRuntimeConsumableParityTests
{
    private const int TestItemId = 2020000;

    [Fact]
    public void TryConsumeInventoryUseItem_ConsumesOnlyTheRequestedLockedSlot()
    {
        InventoryUI inventory = CreateInventory();
        inventory.AddItem(InventoryType.USE, new InventorySlotData
        {
            ItemId = TestItemId,
            Quantity = 1,
            MaxStackSize = 100,
            ItemName = "Potion A",
            IsDisabled = true
        });
        inventory.AddItem(InventoryType.USE, new InventorySlotData
        {
            ItemId = TestItemId,
            Quantity = 3,
            MaxStackSize = 100,
            ItemName = "Potion B"
        });
        inventory.GetSlots(InventoryType.USE)[0].IsDisabled = false;

        bool consumed = MapSimulator.TryConsumeInventoryUseItem(
            inventory,
            InventoryType.USE,
            TestItemId,
            1,
            slotIndex: 0);

        Assert.True(consumed);

        IReadOnlyList<InventorySlotData> slots = inventory.GetSlots(InventoryType.USE);
        Assert.Single(slots);
        Assert.Equal(3, slots[0].Quantity);
        Assert.Equal("Potion B", slots[0].ItemName);
    }

    [Fact]
    public void TryConsumeInventoryUseItem_DoesNotFallbackToAnotherMatchingStackWhenLockedSlotRejects()
    {
        InventoryUI inventory = CreateInventory();
        inventory.AddItem(InventoryType.USE, new InventorySlotData
        {
            ItemId = TestItemId,
            Quantity = 1,
            MaxStackSize = 100,
            ItemName = "Disabled Potion",
            IsDisabled = true
        });
        inventory.AddItem(InventoryType.USE, new InventorySlotData
        {
            ItemId = TestItemId,
            Quantity = 2,
            MaxStackSize = 100,
            ItemName = "Enabled Potion"
        });

        bool consumed = MapSimulator.TryConsumeInventoryUseItem(
            inventory,
            InventoryType.USE,
            TestItemId,
            1,
            slotIndex: 0);

        Assert.False(consumed);

        IReadOnlyList<InventorySlotData> slots = inventory.GetSlots(InventoryType.USE);
        Assert.Equal(2, slots.Count);
        Assert.True(slots[0].IsDisabled);
        Assert.Equal(1, slots[0].Quantity);
        Assert.Equal(2, slots[1].Quantity);
    }

    [Fact]
    public void TryRequestItemUse_PrefersSlotAwareDispatch()
    {
        InventoryUI inventory = CreateInventory();
        inventory.AddItem(InventoryType.USE, new InventorySlotData
        {
            ItemId = TestItemId,
            Quantity = 2,
            MaxStackSize = 100,
            ItemName = "Potion"
        });

        int slotAwareCallCount = 0;
        int legacyCallCount = 0;
        inventory.ItemUseRequestedAtSlot = (itemId, inventoryType, slotIndex) =>
        {
            slotAwareCallCount++;
            Assert.Equal(TestItemId, itemId);
            Assert.Equal(InventoryType.USE, inventoryType);
            Assert.Equal(0, slotIndex);
            return true;
        };
        inventory.ItemUseRequested = (_, _) =>
        {
            legacyCallCount++;
            return true;
        };

        bool handled = inventory.TryRequestItemUse(InventoryType.USE, 0, rightJustPressed: true);

        Assert.True(handled);
        Assert.Equal(1, slotAwareCallCount);
        Assert.Equal(0, legacyCallCount);
    }

    private static InventoryUI CreateInventory()
    {
        return new InventoryUI(frame: null, slotBg: null, device: null);
    }
}
