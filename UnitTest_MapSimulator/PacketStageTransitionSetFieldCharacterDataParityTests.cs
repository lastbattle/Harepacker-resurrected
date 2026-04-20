using System.Reflection;
using HaCreator.MapSimulator.Interaction;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;

namespace UnitTest_MapSimulator;

public sealed class PacketStageTransitionSetFieldCharacterDataParityTests
{
    [Fact]
    public void BackwardUpdateFallback_UsesReplacementWhenNonCashOccupiesOnlySlot()
    {
        var inventoryItems = new Dictionary<InventoryType, IReadOnlyList<PacketCharacterDataItemSlot>>
        {
            [InventoryType.USE] = new[]
            {
                new PacketCharacterDataItemSlot(InventoryType.USE, 1, 2, 2000000, false, 0, 5),
                new PacketCharacterDataItemSlot(InventoryType.USE, 2, 2, 2000001, true, 10001, 7)
            }
        };
        var slotLimits = new Dictionary<InventoryType, int>
        {
            [InventoryType.USE] = 1
        };
        PacketCharacterDataSnapshot snapshot = BuildBackwardUpdateSnapshot(inventoryItems, slotLimits);

        PacketCharacterDataSnapshot decorated = DecorateBackwardUpdateInventoryReconciliation(snapshot);

        Assert.Equal(1, decorated.BackwardUpdatePositionFallbackCashItemCount);
        Assert.Equal(1, decorated.BackwardUpdatePositionSlotOverflowCashItemCount);
        Assert.Equal(0, decorated.BackwardUpdatePositionFallbackInsertedCashItemCount);
        Assert.Equal(1, decorated.BackwardUpdatePositionFallbackReplacementCashItemCount);
        Assert.Equal(1, decorated.BackwardUpdatePositionFallbackReplacementCashItemCountsByType[InventoryType.USE]);
    }

    [Fact]
    public void BackwardUpdateValidatedPosition_CollidesWithNonCashOccupiedSlot()
    {
        var inventoryItems = new Dictionary<InventoryType, IReadOnlyList<PacketCharacterDataItemSlot>>
        {
            [InventoryType.USE] = new[]
            {
                new PacketCharacterDataItemSlot(InventoryType.USE, 1, 2, 2000000, false, 0, 4),
                new PacketCharacterDataItemSlot(InventoryType.USE, 1, 2, 2000002, true, 10002, 8)
            }
        };
        var slotLimits = new Dictionary<InventoryType, int>
        {
            [InventoryType.USE] = 3
        };
        PacketCharacterDataSnapshot snapshot = BuildBackwardUpdateSnapshot(inventoryItems, slotLimits);

        PacketCharacterDataSnapshot decorated = DecorateBackwardUpdateInventoryReconciliation(snapshot);

        Assert.Equal(0, decorated.BackwardUpdatePositionValidatedCashItemCount);
        Assert.Equal(1, decorated.BackwardUpdatePositionFallbackCashItemCount);
        Assert.Equal(1, decorated.BackwardUpdatePositionCollisionCashItemCount);
        Assert.Equal(1, decorated.BackwardUpdatePositionFallbackInsertedCashItemCount);
        Assert.Equal(0, decorated.BackwardUpdatePositionFallbackReplacementCashItemCount);
        Assert.Equal(1, decorated.BackwardUpdatePositionCollisionCashItemCountsByType[InventoryType.USE]);
        Assert.Equal(1, decorated.BackwardUpdatePositionFallbackInsertedCashItemCountsByType[InventoryType.USE]);
    }

    private static PacketCharacterDataSnapshot BuildBackwardUpdateSnapshot(
        IReadOnlyDictionary<InventoryType, IReadOnlyList<PacketCharacterDataItemSlot>> inventoryItems,
        IReadOnlyDictionary<InventoryType, int> slotLimits)
    {
        return new PacketCharacterDataSnapshot(
            1,
            "ParityTester",
            0,
            0,
            20000,
            30000,
            1,
            0,
            4,
            4,
            4,
            4,
            50,
            50,
            5,
            5,
            0,
            0,
            0,
            0,
            0,
            100000000,
            0,
            0,
            0,
            0,
            string.Empty,
            HasBackwardUpdate: true,
            InventorySlotLimits: slotLimits,
            InventoryItemsByType: inventoryItems);
    }

    private static PacketCharacterDataSnapshot DecorateBackwardUpdateInventoryReconciliation(PacketCharacterDataSnapshot snapshot)
    {
        MethodInfo method = typeof(PacketStageTransitionRuntime).GetMethod(
            "DecorateBackwardUpdateInventoryReconciliation",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Backward-update reconciliation helper method was not found.");

        object result = method.Invoke(null, new object[] { snapshot })
            ?? throw new InvalidOperationException("Backward-update reconciliation helper returned null.");
        return (PacketCharacterDataSnapshot)result;
    }
}
