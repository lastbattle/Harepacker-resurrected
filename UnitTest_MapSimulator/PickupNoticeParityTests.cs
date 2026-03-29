using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.UI;

namespace UnitTest_MapSimulator;

public sealed class PickupNoticeParityTests
{
    [Fact]
    public void TryPickUpDropByPetDetailed_UsesRecentPickupForUnavailableFailures()
    {
        var pool = new DropPool();
        pool.Initialize();

        DropItem drop = pool.SpawnItemDrop(0f, 0f, "2000000", 1, 1);
        drop.State = DropState.Idle;
        drop.CanPickup = true;

        const int remotePetId = 73;
        const string remotePetName = "Remote Jr. Reaper";
        const int pickupTime = 2500;

        bool resolved = pool.ResolveRemotePickup(
            drop,
            remotePetId,
            pickupTime,
            DropPickupActorKind.Pet,
            remotePetName,
            pickedByPet: true);

        Assert.True(resolved);

        DropPickupAttemptResult result = pool.TryPickUpDropByPetDetailed(
            petId: 9,
            petX: 0f,
            petY: 0f,
            playerId: 1,
            currentTime: pickupTime + 1,
            petPickupRange: 120f);

        Assert.Null(result.Drop);
        Assert.Equal(DropPickupFailureReason.Unavailable, result.FailureReason);
        Assert.NotNull(result.RecentPickup);
        Assert.Equal(DropPickupActorKind.Pet, result.RecentPickup.ActorKind);
        Assert.Equal(remotePetId, result.RecentPickup.PickerId);
        Assert.Equal(remotePetName, result.RecentPickup.ActorName);
    }

    [Fact]
    public void FormatFailure_UsesRecordedPetActorNameForUnavailableFailures()
    {
        var recentPickup = new RecentPickupRecord
        {
            ActorKind = DropPickupActorKind.Pet,
            ActorName = "Remote Jr. Reaper",
            PickerId = 73,
            Type = DropType.Item,
            ItemId = "2000000",
            Quantity = 1
        };

        PickupNoticeMessagePair messagePair = PickupNoticeTextFormatter.FormatFailure(
            DropPickupFailureReason.Unavailable,
            itemName: "Red Potion",
            dropType: DropType.Item,
            quantity: 1,
            recentPickup: recentPickup,
            recentActorName: recentPickup.ActorName);

        Assert.Equal("A pet picked up the drop.", messagePair.ScreenMessage);
        Assert.Equal("Remote Jr. Reaper picked up Red Potion.", messagePair.ChatMessage);
    }
}
