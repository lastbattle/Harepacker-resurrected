using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;

namespace UnitTest_MapSimulator;

public sealed class SocialRoomParityTests
{
    [Fact]
    public void TradingRoomRemoteInventoryOfferConsumesAndResetRestoresDefaults()
    {
        SocialRoomRuntime runtime = SocialRoomRuntime.CreateTradingRoomSample();

        bool offeredItem = runtime.TryOfferRemoteTradeItem(4004004, 10, out string? itemMessage);
        bool offeredMeso = runtime.TryOfferRemoteTradeMeso(50000, out string? mesoMessage);

        Assert.True(offeredItem, itemMessage);
        Assert.True(offeredMeso, mesoMessage);
        Assert.Equal(275000, runtime.RemoteInventoryMeso);
        Assert.Contains(runtime.RemoteInventoryEntries, entry => entry.ItemId == 4004004 && entry.Quantity == 30);

        runtime.ResetTrade();

        Assert.Equal(325000, runtime.RemoteInventoryMeso);
        Assert.Contains(runtime.RemoteInventoryEntries, entry => entry.ItemId == 4004004 && entry.Quantity == 40);
        Assert.Contains(runtime.Items, entry => entry.OwnerName == "Rondo");
    }

    [Fact]
    public void TradingRoomSnapshotRoundTripsThroughPersistenceStore()
    {
        string tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-social-room.json");

        try
        {
            SocialRoomRuntime source = SocialRoomRuntime.CreateTradingRoomSample();
            Assert.True(source.TryOfferRemoteTradeItem(4004004, 5, out string? itemMessage), itemMessage);
            Assert.True(source.TryOfferRemoteTradeMeso(25000, out string? mesoMessage), mesoMessage);

            SocialRoomPersistenceStore store = new(tempFilePath);
            SocialRoomRuntimeSnapshot snapshot = source.BuildSnapshot();
            store.Save("trade", snapshot);

            SocialRoomRuntimeSnapshot? loadedSnapshot = store.Load("trade");
            SocialRoomRuntime restored = SocialRoomRuntime.CreateTradingRoomSample();
            restored.RestoreSnapshot(loadedSnapshot);

            Assert.NotNull(loadedSnapshot);
            Assert.Equal(snapshot.RoomState, restored.RoomState);
            Assert.Equal(snapshot.TradeRemoteOfferMeso, restored.BuildSnapshot().TradeRemoteOfferMeso);
            Assert.Equal(snapshot.RemoteInventoryMeso, restored.RemoteInventoryMeso);
            Assert.Contains(restored.RemoteInventoryEntries, entry => entry.ItemId == 4004004 && entry.Quantity == 35);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }
}
