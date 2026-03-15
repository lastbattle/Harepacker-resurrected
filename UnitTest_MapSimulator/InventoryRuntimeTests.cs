using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;

namespace UnitTest_MapSimulator
{
    public class InventoryRuntimeTests
    {
        [Fact]
        public void AddItem_SplitsStacksUsingMaxStackBeforeAddingNewSlots()
        {
            TestInventoryUI inventory = new TestInventoryUI();

            inventory.AddItem(InventoryType.ETC, new InventorySlotData
            {
                ItemId = 4000000,
                Quantity = 150,
                MaxStackSize = 100
            });

            Assert.Equal(150, inventory.GetItemCount(InventoryType.ETC, 4000000));
            Assert.Equal(2, inventory.GetSlotCount(InventoryType.ETC));
            Assert.Equal(new[] { 100, 50 }, inventory.GetSlotQuantities(InventoryType.ETC));
        }

        [Fact]
        public void AddItem_FillsExistingStacksBeforeAllocatingAnotherSlot()
        {
            TestInventoryUI inventory = new TestInventoryUI();

            inventory.AddItem(InventoryType.ETC, new InventorySlotData
            {
                ItemId = 4000001,
                Quantity = 80,
                MaxStackSize = 100
            });
            inventory.AddItem(InventoryType.ETC, new InventorySlotData
            {
                ItemId = 4000001,
                Quantity = 40,
                MaxStackSize = 100
            });

            Assert.Equal(120, inventory.GetItemCount(InventoryType.ETC, 4000001));
            Assert.Equal(2, inventory.GetSlotCount(InventoryType.ETC));
            Assert.Equal(new[] { 100, 20 }, inventory.GetSlotQuantities(InventoryType.ETC));
        }

        [Fact]
        public void AddItem_DropsOverflowWhenNoFreeSlotsRemain()
        {
            TestInventoryUI inventory = new TestInventoryUI();
            inventory.SetSlotLimit(InventoryType.USE, 24);

            inventory.AddItem(InventoryType.USE, new InventorySlotData
            {
                ItemId = 2000000,
                Quantity = 2600,
                MaxStackSize = 100
            });

            Assert.Equal(2400, inventory.GetItemCount(InventoryType.USE, 2000000));
            Assert.Equal(24, inventory.GetSlotCount(InventoryType.USE));
        }

        private sealed class TestInventoryUI : InventoryUI
        {
            public TestInventoryUI()
                : base(null, null, null)
            {
            }

            public int GetSlotCount(InventoryType type)
            {
                return TryGetSlotsForType(type, out var slots) ? slots.Count : 0;
            }

            public int[] GetSlotQuantities(InventoryType type)
            {
                if (!TryGetSlotsForType(type, out var slots))
                {
                    return [];
                }

                return slots.ConvertAll(slot => slot.Quantity).ToArray();
            }
        }
    }
}
