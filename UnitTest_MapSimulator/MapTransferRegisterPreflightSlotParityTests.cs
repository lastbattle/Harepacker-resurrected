using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Managers;

namespace UnitTest_MapSimulator;

public sealed class MapTransferRegisterPreflightSlotParityTests
{
    [Fact]
    public void HasMapTransferEmptySlot_ReturnsTrueWhenSnapshotContainsSentinel()
    {
        int[] snapshot =
        {
            100000000,
            MapTransferRuntimeManager.EmptyDestinationMapId,
            101000000,
            102000000,
            103000000
        };

        Assert.True(MapSimulator.HasMapTransferEmptySlot(snapshot));
    }

    [Fact]
    public void HasMapTransferEmptySlot_ReturnsFalseWhenSnapshotHasNoSentinel()
    {
        int[] snapshot =
        {
            100000000,
            101000000,
            102000000,
            103000000,
            104000000
        };

        Assert.False(MapSimulator.HasMapTransferEmptySlot(snapshot));
    }

    [Fact]
    public void TryFindMapTransferSlot_ReturnsMatchedSlotIndex()
    {
        int[] snapshot =
        {
            100000000,
            101000000,
            102000000,
            MapTransferRuntimeManager.EmptyDestinationMapId,
            104000000
        };

        Assert.True(MapSimulator.TryFindMapTransferSlot(snapshot, 102000000, out int slotIndex));
        Assert.Equal(2, slotIndex);
    }

    [Fact]
    public void TryFindMapTransferSlot_ReturnsFalseForUnknownMap()
    {
        int[] snapshot =
        {
            100000000,
            101000000,
            102000000,
            MapTransferRuntimeManager.EmptyDestinationMapId,
            104000000
        };

        Assert.False(MapSimulator.TryFindMapTransferSlot(snapshot, 200000000, out int slotIndex));
        Assert.Equal(-1, slotIndex);
    }
}
