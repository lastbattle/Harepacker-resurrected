using System.Collections.Generic;
using HaCreator.MapSimulator;

namespace UnitTest_MapSimulator;

public class RemoteDropPacketPartyOwnershipParityTests
{
    [Fact]
    public void IsObservedDropPartyActorPartyLinked_AdmitsTransitiveKnownPartyComponent()
    {
        Dictionary<int, int> actorParents = new();
        MapSimulator.RegisterObservedDropPartyActorLink(actorParents, 1001, 2001);
        MapSimulator.RegisterObservedDropPartyActorLink(actorParents, 2001, 3001);

        bool linked = MapSimulator.IsObservedDropPartyActorPartyLinked(
            actorParents,
            actorId: 3001,
            localPartyId: 0,
            localCharacterId: 0,
            trackedPartyActorEvaluator: actorId => actorId == 1001,
            legacyTrackedActorEvaluator: _ => false);

        Assert.True(linked);
    }

    [Fact]
    public void IsObservedDropPartyActorPartyLinked_RejectsUnknownOnlyObservedComponent()
    {
        Dictionary<int, int> actorParents = new();
        MapSimulator.RegisterObservedDropPartyActorLink(actorParents, 4101, 4102);

        bool linked = MapSimulator.IsObservedDropPartyActorPartyLinked(
            actorParents,
            actorId: 4102,
            localPartyId: 0,
            localCharacterId: 0,
            trackedPartyActorEvaluator: _ => false,
            legacyTrackedActorEvaluator: _ => false);

        Assert.False(linked);
    }

    [Fact]
    public void AreDropActorsInSameParty_AdmitsLocalPartyOwnerViaObservedComponentLinkage()
    {
        Dictionary<int, int> actorParents = new();
        MapSimulator.RegisterObservedDropPartyActorLink(actorParents, 777, 9001);
        MapSimulator.RegisterObservedDropPartyActorLink(actorParents, 9001, 9002);

        bool sameParty = MapSimulator.AreDropActorsInSameParty(
            ownerId: 777,
            actorId: 9002,
            localPartyId: 777,
            localCharacterId: 42,
            trackedPartyActorEvaluator: _ => false,
            legacyTrackedActorEvaluator: _ => false,
            observedPartyLinkEvaluator: (first, second) => MapSimulator.AreObservedDropPartyActorsLinked(actorParents, first, second),
            observedPartyAnchorEvaluator: _ => false,
            partyActorOwnerResolver: actorId => actorId,
            observedPartyLinkedEvaluator: actorId => MapSimulator.IsObservedDropPartyActorPartyLinked(
                actorParents,
                actorId,
                localPartyId: 777,
                localCharacterId: 42,
                trackedPartyActorEvaluator: _ => false,
                legacyTrackedActorEvaluator: _ => false));

        Assert.True(sameParty);
    }
}
