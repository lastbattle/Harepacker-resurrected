using HaCreator.MapSimulator.Pools;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator;

public sealed class DropPoolPacketParityTests
{
    [Fact]
    public void ApplyPacketLeave_RemotePlayerPickup_RefreshesAbsorbTargetDuringPickup()
    {
        var pool = new DropPool();
        pool.Initialize();

        const int currentTime = 1000;
        Assert.True(pool.ApplyPacketEnter(
            new RemoteDropEnterPacket(
                EnterType: 2,
                DropId: 100,
                IsMoney: false,
                Info: 2000000,
                OwnerId: 0,
                OwnershipType: DropOwnershipType.None,
                TargetX: 0,
                TargetY: 0,
                SourceId: 0,
                HasStartPosition: false,
                StartX: 0,
                StartY: 0,
                DelayMs: 0,
                AllowPetPickup: true,
                ElevateLayer: false),
            currentTime));

        DropItem drop = pool.GetDrop(100);
        Assert.NotNull(drop);

        Vector2 actorPosition = new(20f, -10f);
        Assert.True(pool.ApplyPacketLeave(
            new RemoteDropLeavePacket(PacketDropLeaveReason.PlayerPickup, 100, 900, 0, 0),
            currentTime,
            localCharacterId: 1,
            actorPositionResolver: (_, _) => actorPosition));

        actorPosition = new Vector2(120f, -40f);
        drop.Update(currentTime + (DropItem.PACKET_ABSORB_DURATION / 2), 0f);

        Assert.Equal(DropState.PickingUp, drop.State);
        Assert.Equal(actorPosition.X, drop.PickupTargetX);
        Assert.Equal(actorPosition.Y, drop.PickupTargetY);
        Assert.True(drop.X > 100f);
        Assert.True(drop.Y < -20f);
    }

    [Fact]
    public void ApplyPacketLeave_LocalPetPickup_UsesResolvedPetRuntimeIdAndLiveTarget()
    {
        var pool = new DropPool();
        pool.Initialize();

        const int currentTime = 2000;
        const int localCharacterId = 321;
        const int resolvedPetRuntimeId = 7777;
        Assert.True(pool.ApplyPacketEnter(
            new RemoteDropEnterPacket(
                EnterType: 2,
                DropId: 200,
                IsMoney: true,
                Info: 123,
                OwnerId: localCharacterId,
                OwnershipType: DropOwnershipType.Character,
                TargetX: 10,
                TargetY: 15,
                SourceId: 0,
                HasStartPosition: false,
                StartX: 0,
                StartY: 0,
                DelayMs: 0,
                AllowPetPickup: true,
                ElevateLayer: false),
            currentTime));

        DropItem drop = pool.GetDrop(200);
        Assert.NotNull(drop);

        Vector2 petPosition = new(32f, 18f);
        Assert.True(pool.ApplyPacketLeave(
            new RemoteDropLeavePacket(PacketDropLeaveReason.PetPickup, 200, localCharacterId, 0, 2),
            currentTime,
            localCharacterId,
            actorPositionResolver: (_, _) => petPosition,
            petPickupActorIdResolver: _ => resolvedPetRuntimeId));

        RecentPickupRecord recentPickup = Assert.Single(pool.GetRecentPickups());
        Assert.Equal(resolvedPetRuntimeId, recentPickup.PickerId);
        Assert.True(recentPickup.PickedByPet);
        Assert.Equal(DropPickupActorKind.Pet, recentPickup.ActorKind);

        petPosition = new Vector2(90f, 60f);
        drop.Update(currentTime + (DropItem.PACKET_ABSORB_DURATION / 2), 0f);

        Assert.Equal(petPosition.X, drop.PickupTargetX);
        Assert.Equal(petPosition.Y, drop.PickupTargetY);
        Assert.True(drop.X > 70f);
        Assert.True(drop.Y > 45f);
    }
}
