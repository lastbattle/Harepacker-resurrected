using System.Collections.Generic;
using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Pools;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator;

public class RemoteDropPacketPetPickupPositionParityTests
{
    [Fact]
    public void ResolveRemoteUserDropPickupActorId_PetOwnerActorNormalizesToEncodedSlot()
    {
        var packet = new RemoteUserDropPickupPacket(
            DropId: 1,
            ActorId: 3000,
            ActorKind: DropPickupActorKind.Pet,
            ActorName: null,
            FallbackOwnerId: 3000);

        int resolvedActorId = MapSimulator.ResolveRemoteUserDropPickupActorId(packet, remotePetItemIds: null);

        Assert.Equal(MapSimulator.BuildRemotePetPickupActorId(3000, 0), resolvedActorId);
    }

    [Fact]
    public void ResolveRemoteUserDropPickupActorId_PreservesExplicitNonOwnerActorId()
    {
        var packet = new RemoteUserDropPickupPacket(
            DropId: 1,
            ActorId: 5555,
            ActorKind: DropPickupActorKind.Pet,
            ActorName: null,
            FallbackOwnerId: 3000);

        int resolvedActorId = MapSimulator.ResolveRemoteUserDropPickupActorId(packet, new[] { 5000001, 0, 0 });

        Assert.Equal(5555, resolvedActorId);
    }

    [Fact]
    public void ResolveRemoteUserDropPickupActorId_UsesClosestPopulatedSlotWhenSlotZeroMissing()
    {
        var packet = new RemoteUserDropPickupPacket(
            DropId: 1,
            ActorId: 3000,
            ActorKind: DropPickupActorKind.Pet,
            ActorName: null,
            FallbackOwnerId: 3000);

        int resolvedActorId = MapSimulator.ResolveRemoteUserDropPickupActorId(packet, new[] { 0, 0, 5000001 });

        Assert.Equal(MapSimulator.BuildRemotePetPickupActorId(3000, 2), resolvedActorId);
    }

    [Fact]
    public void TryResolveRemotePetPickupSlotIndexForPacketParity_ClampsSyntheticSlotWhenPetIdsMissing()
    {
        bool resolved = MapSimulator.TryResolveRemotePetPickupSlotIndexForPacketParity(
            remotePetItemIds: null,
            slotIndex: 8,
            out int resolvedSlotIndex);

        Assert.True(resolved);
        Assert.Equal(2, resolvedSlotIndex);
    }

    [Fact]
    public void ResolveObservedRemotePetPickupPosition_UsesClosestOwnerSlotFallback()
    {
        Dictionary<int, Vector2> observedPetActorPositions = new()
        {
            [MapSimulator.BuildRemotePetPickupActorId(7001, 2)] = new Vector2(100f, 200f)
        };

        Vector2? resolved = MapSimulator.ResolveObservedRemotePetPickupPosition(
            observedPetActorPositions,
            ownerCharacterId: 7001,
            slotIndex: 0);

        Assert.True(resolved.HasValue);
        Assert.Equal(new Vector2(100f, 200f), resolved.Value);
    }
}
