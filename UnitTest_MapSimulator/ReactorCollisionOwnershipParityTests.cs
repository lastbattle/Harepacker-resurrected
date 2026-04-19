using HaCreator.MapSimulator.Pools;
using System.Collections.Generic;
using Xunit;

namespace UnitTest_MapSimulator
{
    public sealed class ReactorCollisionOwnershipParityTests
    {
        [Fact]
        public void PacketEnterSelector_RefusesSingleCandidateWhenPacketNamePresentButUnresolved()
        {
            List<PacketEnterAuthoredReactorCandidate> candidates = new()
            {
                new PacketEnterAuthoredReactorCandidate(
                    Index: 7,
                    IsPacketNamePresent: true,
                    IsLocallyTouched: true,
                    ContainsCurrentLocalUserPosition: true,
                    MatchesPacketName: false,
                    HasExactNameMatch: false,
                    VisualState: 0)
            };

            bool selected = ReactorPool.TrySelectAuthoredReactorCandidateForPacketEnter(
                candidates,
                initialState: 0,
                out int _,
                out PacketEnterAuthoredReactorSelectionReason reason);

            Assert.False(selected);
            Assert.Equal(PacketEnterAuthoredReactorSelectionReason.None, reason);
        }

        [Fact]
        public void PacketEnterSelector_AllowsSingleCandidateWhenPacketNameMatches()
        {
            List<PacketEnterAuthoredReactorCandidate> candidates = new()
            {
                new PacketEnterAuthoredReactorCandidate(
                    Index: 7,
                    IsPacketNamePresent: true,
                    IsLocallyTouched: false,
                    ContainsCurrentLocalUserPosition: false,
                    MatchesPacketName: true,
                    HasExactNameMatch: true,
                    VisualState: 0)
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
    }
}
