using System.Collections.Generic;
using HaCreator.MapSimulator;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator;

public sealed class RemoteDropPacketPetPickupPositionParityTests
{
    [Fact]
    public void ResolveObservedRemotePetPickupPosition_FallsBackToClosestObservedSlotForOwner()
    {
        const int ownerId = 3000;
        Dictionary<int, Vector2> observedPositions = new()
        {
            [MapSimulator.BuildRemotePetPickupActorId(ownerId, 0)] = new Vector2(10f, 20f),
            [MapSimulator.BuildRemotePetPickupActorId(ownerId, 2)] = new Vector2(30f, 40f)
        };

        Vector2? resolved = MapSimulator.ResolveObservedRemotePetPickupPosition(observedPositions, ownerId, 1);

        Assert.True(resolved.HasValue);
        Assert.Equal(new Vector2(10f, 20f), resolved.Value);
    }

    [Fact]
    public void TryResolveRemotePetPickupPositionFromOwnerState_AdmitsSyntheticThreeSlotSurfaceWhenItemsMissing()
    {
        bool resolved = MapSimulator.TryResolveRemotePetPickupPositionFromOwnerState(
            new Vector2(100f, 200f),
            ownerFacingRight: true,
            remotePetItemIds: null,
            slotIndex: 2,
            out Vector2 position);

        Assert.True(resolved);
        Assert.Equal(new Vector2(36f, 200f), position);
    }

    [Fact]
    public void RemoveObservedRemotePetPickupOwner_RemovesObservedEntriesForOwnerOnly()
    {
        Dictionary<int, Vector2> observedPositions = new()
        {
            [MapSimulator.BuildRemotePetPickupActorId(3000, 0)] = new Vector2(10f, 10f),
            [MapSimulator.BuildRemotePetPickupActorId(3000, 1)] = new Vector2(20f, 20f),
            [MapSimulator.BuildRemotePetPickupActorId(4000, 0)] = new Vector2(30f, 30f)
        };

        MapSimulator.RemoveObservedRemotePetPickupOwner(observedPositions, 3000);

        Assert.Single(observedPositions);
        Assert.True(observedPositions.ContainsKey(MapSimulator.BuildRemotePetPickupActorId(4000, 0)));
    }
}
