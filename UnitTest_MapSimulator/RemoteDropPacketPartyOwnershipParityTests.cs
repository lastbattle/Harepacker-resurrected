using System;
using System.Collections.Generic;
using HaCreator.MapSimulator;

namespace UnitTest_MapSimulator;

public sealed class RemoteDropPacketPartyOwnershipParityTests
{
    [Fact]
    public void AreDropActorsInSameParty_AdmitsObservedComponentLinkedToKnownPartyActor()
    {
        const int localCharacterId = 1000;
        const int localPartyId = 2000;
        const int ownerId = 7000;
        const int actorId = 7100;

        Dictionary<int, int> observedLinks = new();
        MapSimulator.RegisterObservedDropPartyActorLink(observedLinks, ownerId, actorId);
        MapSimulator.RegisterObservedDropPartyActorLink(observedLinks, ownerId, localPartyId);

        bool result = MapSimulator.AreDropActorsInSameParty(
            ownerId,
            actorId,
            localPartyId,
            localCharacterId,
            _ => false,
            _ => false,
            (left, right) => MapSimulator.AreObservedDropPartyActorsLinked(observedLinks, left, right),
            _ => false,
            _ => 0,
            actor => MapSimulator.IsObservedDropPartyActorPartyLinked(
                observedLinks,
                actor,
                localPartyId,
                localCharacterId,
                _ => false,
                _ => false,
                _ => false,
                _ => 0));

        Assert.True(result);
    }

    [Fact]
    public void AreDropActorsInSameParty_RefusesUnknownOnlyObservedLinkEvenWhenLinked()
    {
        const int localCharacterId = 1000;
        const int localPartyId = 2000;
        const int ownerId = 7000;
        const int actorId = 7100;

        Dictionary<int, int> observedLinks = new();
        MapSimulator.RegisterObservedDropPartyActorLink(observedLinks, ownerId, actorId);

        bool result = MapSimulator.AreDropActorsInSameParty(
            ownerId,
            actorId,
            localPartyId,
            localCharacterId,
            _ => false,
            _ => false,
            (left, right) => MapSimulator.AreObservedDropPartyActorsLinked(observedLinks, left, right),
            _ => false,
            _ => 0,
            actor => MapSimulator.IsObservedDropPartyActorPartyLinked(
                observedLinks,
                actor,
                localPartyId,
                localCharacterId,
                _ => false,
                _ => false,
                _ => false,
                _ => 0));

        Assert.False(result);
    }

    [Fact]
    public void AreDropActorsInSameParty_LocalPartyIdOwnerAdmitsTransitiveObservedPartyLinkedActor()
    {
        const int localCharacterId = 1000;
        const int localPartyId = 2000;
        const int actorId = 7100;

        Dictionary<int, int> observedLinks = new();
        MapSimulator.RegisterObservedDropPartyActorLink(observedLinks, actorId, 7900);
        MapSimulator.RegisterObservedDropPartyActorLink(observedLinks, 7900, localPartyId);

        bool result = MapSimulator.AreDropActorsInSameParty(
            localPartyId,
            actorId,
            localPartyId,
            localCharacterId,
            _ => false,
            _ => false,
            (left, right) => MapSimulator.AreObservedDropPartyActorsLinked(observedLinks, left, right),
            _ => false,
            _ => 0,
            actor => MapSimulator.IsObservedDropPartyActorPartyLinked(
                observedLinks,
                actor,
                localPartyId,
                localCharacterId,
                _ => false,
                _ => false,
                _ => false,
                _ => 0));

        Assert.True(result);
    }
}
