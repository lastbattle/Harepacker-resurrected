using System.Collections.Generic;
using HaCreator.MapSimulator.Pools;
using Xunit;

namespace UnitTest_MapSimulator;

public class ReactorCollisionOwnershipParityTests
{
    [Fact]
    public void TrySelectAuthoredReactorCandidateForPacketEnter_UnresolvedPacketName_MultiCandidateCanResolveByClientSignals()
    {
        var candidates = new List<PacketEnterAuthoredReactorCandidate>
        {
            new(
                Index: 3,
                IsPacketNamePresent: true,
                IsLocallyTouched: false,
                ContainsCurrentLocalUserPosition: false,
                MatchesPacketName: false,
                HasExactNameMatch: false,
                VisualState: 1),
            new(
                Index: 7,
                IsPacketNamePresent: true,
                IsLocallyTouched: true,
                ContainsCurrentLocalUserPosition: false,
                MatchesPacketName: false,
                HasExactNameMatch: false,
                VisualState: 1)
        };

        bool selected = ReactorPool.TrySelectAuthoredReactorCandidateForPacketEnter(
            candidates,
            initialState: 0,
            out int index,
            out PacketEnterAuthoredReactorSelectionReason reason);

        Assert.True(selected);
        Assert.Equal(7, index);
        Assert.Equal(PacketEnterAuthoredReactorSelectionReason.ClientSignal, reason);
    }

    [Fact]
    public void TrySelectAuthoredReactorCandidateForPacketEnter_UnresolvedPacketName_DisjointSignalsCanUseNarrowedWzFallback()
    {
        var candidates = new List<PacketEnterAuthoredReactorCandidate>
        {
            new(
                Index: 11,
                IsPacketNamePresent: true,
                IsLocallyTouched: true,
                ContainsCurrentLocalUserPosition: false,
                MatchesPacketName: false,
                HasExactNameMatch: false,
                VisualState: 5),
            new(
                Index: 4,
                IsPacketNamePresent: true,
                IsLocallyTouched: false,
                ContainsCurrentLocalUserPosition: false,
                MatchesPacketName: false,
                HasExactNameMatch: false,
                VisualState: 2)
        };

        bool selected = ReactorPool.TrySelectAuthoredReactorCandidateForPacketEnter(
            candidates,
            initialState: 2,
            out int index,
            out PacketEnterAuthoredReactorSelectionReason reason);

        Assert.True(selected);
        Assert.Equal(4, index);
        Assert.Equal(PacketEnterAuthoredReactorSelectionReason.WzAuthoredOrderFallback, reason);
    }

    [Fact]
    public void TrySelectAuthoredReactorCandidateForPacketEnter_UnresolvedPacketName_FullyAmbiguousCanUseWzOrderFallback()
    {
        var candidates = new List<PacketEnterAuthoredReactorCandidate>
        {
            new(
                Index: 9,
                IsPacketNamePresent: true,
                IsLocallyTouched: false,
                ContainsCurrentLocalUserPosition: false,
                MatchesPacketName: false,
                HasExactNameMatch: false,
                VisualState: 3),
            new(
                Index: 2,
                IsPacketNamePresent: true,
                IsLocallyTouched: false,
                ContainsCurrentLocalUserPosition: false,
                MatchesPacketName: false,
                HasExactNameMatch: false,
                VisualState: 3)
        };

        bool selected = ReactorPool.TrySelectAuthoredReactorCandidateForPacketEnter(
            candidates,
            initialState: 1,
            out int index,
            out PacketEnterAuthoredReactorSelectionReason reason);

        Assert.True(selected);
        Assert.Equal(2, index);
        Assert.Equal(PacketEnterAuthoredReactorSelectionReason.WzAuthoredOrderFallback, reason);
    }
}
