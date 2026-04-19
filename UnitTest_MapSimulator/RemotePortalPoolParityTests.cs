using HaCreator.MapSimulator.Fields;
using MapleLib.WzLib.WzStructure.Data;
using Xunit;

namespace UnitTest_MapSimulator
{
    public sealed class RemotePortalPoolParityTests
    {
        [Fact]
        public void ClearRemoteTownPortalInferenceState_ClearsAllBuckets()
        {
            (bool metadataCleared, bool observationsCleared, bool pendingCleared) = TemporaryPortalField.ClearRemoteTownPortalInferenceStateForTesting();

            Assert.True(metadataCleared);
            Assert.True(observationsCleared);
            Assert.True(pendingCleared);
        }

        [Fact]
        public void ExistingStableTownPortalCreate_DoesNotReenterOpening()
        {
            TemporaryPortalField.RemoteTownPortalVisualPhase phase = TemporaryPortalField.ResolveRemoteTownPortalCreatePhaseForTesting(
                packetState: 1,
                hasExistingState: true,
                existingPacketState: 1,
                existingPhase: TemporaryPortalField.RemoteTownPortalVisualPhase.Stable);

            Assert.Equal(TemporaryPortalField.RemoteTownPortalVisualPhase.Stable, phase);
        }

        [Fact]
        public void ExistingOpenStateTownPortalCreate_WithZeroPacketState_StillKeepsStable()
        {
            TemporaryPortalField.RemoteTownPortalVisualPhase phase = TemporaryPortalField.ResolveRemoteTownPortalCreatePhaseForTesting(
                packetState: 0,
                hasExistingState: true,
                existingPacketState: 1,
                existingPhase: TemporaryPortalField.RemoteTownPortalVisualPhase.Stable);

            Assert.Equal(TemporaryPortalField.RemoteTownPortalVisualPhase.Stable, phase);
        }

        [Fact]
        public void ExistingStateZeroTownPortalCreate_PromotesOverlayState()
        {
            byte resolved = TemporaryPortalField.ResolveRemoteTownPortalCreateStateForTesting(
                packetState: 0,
                hasExistingState: true,
                existingPacketState: 0,
                existingPhase: TemporaryPortalField.RemoteTownPortalVisualPhase.Stable);

            Assert.Equal(1, resolved);
        }

        [Fact]
        public void SourceSelection_HoldsExistingInferredDestinationAgainstWeakerUnrelatedObservation()
        {
            TemporaryPortalField.RemoteTownPortalResolvedDestination? destination = TemporaryPortalField.ResolveRemoteTownPortalDestinationFromObservedCandidatesForTesting(
                currentMapId: 100000000,
                hasIncomingDestination: false,
                incomingDestinationMapId: 0,
                incomingDestinationX: 0,
                incomingDestinationY: 0,
                hasExistingDestination: true,
                existingDestinationMapId: 101000000,
                existingDestinationX: 10,
                existingDestinationY: 20,
                hasMetadata: false,
                metadataSourceMapId: 0,
                metadataSourceX: 0,
                metadataSourceY: 0,
                metadataTownMapId: 0,
                metadataObservationSource: TemporaryPortalField.RemoteTownPortalObservationSource.WzReturnMapFallback,
                metadataRecordedAt: 0,
                (SourceMapId: 102000000, SourceX: 30f, SourceY: 40f, TownMapId: 100000000, ObservationSource: TemporaryPortalField.RemoteTownPortalObservationSource.MovementSnapshot, RecordedAt: 1));

            Assert.True(destination.HasValue);
            Assert.Equal(101000000, destination.Value.MapId);
            Assert.Equal(10f, destination.Value.X);
            Assert.Equal(20f, destination.Value.Y);
        }

        [Fact]
        public void SourceSelection_ReplacesExistingDestinationWithStrongerSameTownEvidence()
        {
            TemporaryPortalField.RemoteTownPortalResolvedDestination? destination = TemporaryPortalField.ResolveRemoteTownPortalDestinationFromObservedCandidatesForTesting(
                currentMapId: 100000000,
                hasIncomingDestination: false,
                incomingDestinationMapId: 0,
                incomingDestinationX: 0,
                incomingDestinationY: 0,
                hasExistingDestination: true,
                existingDestinationMapId: 101000000,
                existingDestinationX: 10,
                existingDestinationY: 20,
                hasMetadata: false,
                metadataSourceMapId: 0,
                metadataSourceX: 0,
                metadataSourceY: 0,
                metadataTownMapId: 0,
                metadataObservationSource: TemporaryPortalField.RemoteTownPortalObservationSource.WzReturnMapFallback,
                metadataRecordedAt: 0,
                (SourceMapId: 102000000, SourceX: 55f, SourceY: 66f, TownMapId: 100000000, ObservationSource: TemporaryPortalField.RemoteTownPortalObservationSource.EnterField, RecordedAt: 2));

            Assert.True(destination.HasValue);
            Assert.Equal(102000000, destination.Value.MapId);
            Assert.Equal(55f, destination.Value.X);
            Assert.Equal(66f, destination.Value.Y);
        }

        [Fact]
        public void PreferredSourceWzFallback_WhenValidatedWithoutPosition_RemainsUnresolved()
        {
            TemporaryPortalField.RemoteTownPortalResolvedDestination? destination =
                TemporaryPortalField.ResolveRemoteTownPortalWzFallbackDestinationWithPreferredSourceForTesting(
                    townMapId: 100000000,
                    hasPreferredSourceMap: true,
                    preferredSourceMapId: 101000000,
                    preferredSourceResolvesToTownMap: true,
                    preferredSourceHasPosition: false,
                    preferredSourceX: 0,
                    preferredSourceY: 0,
                    (SourceMapId: 102000000, ReturnMapId: 100000000, ForcedReturnMapId: 0, HasSourcePosition: true, SourceX: 300, SourceY: 400));

            Assert.False(destination.HasValue);
        }

        [Fact]
        public void UniqueWzFallback_ResolvesWhenNoPreferredSourceExists()
        {
            TemporaryPortalField.RemoteTownPortalResolvedDestination? destination =
                TemporaryPortalField.ResolveRemoteTownPortalWzFallbackDestinationWithPreferredSourceForTesting(
                    townMapId: 100000000,
                    hasPreferredSourceMap: false,
                    preferredSourceMapId: 0,
                    preferredSourceResolvesToTownMap: false,
                    preferredSourceHasPosition: false,
                    preferredSourceX: 0,
                    preferredSourceY: 0,
                    (SourceMapId: 102000000, ReturnMapId: 100000000, ForcedReturnMapId: 0, HasSourcePosition: true, SourceX: 300, SourceY: 400));

            Assert.True(destination.HasValue);
            Assert.Equal(102000000, destination.Value.MapId);
            Assert.Equal(300f, destination.Value.X);
            Assert.Equal(400f, destination.Value.Y);
        }

        [Fact]
        public void PreferredSourceSelection_UsesExistingInferredSource_WhenOwnerObservationsAreMissing()
        {
            int? preferredSourceMapId = TemporaryPortalField.ResolvePreferredRemoteTownPortalSourceMapIdForTesting(
                currentMapId: 100000000,
                hasExistingDestination: true,
                existingDestinationMapId: 101000000,
                existingDestinationX: 50f,
                existingDestinationY: 60f,
                hasMetadata: false,
                metadataSourceMapId: 0,
                metadataSourceX: 0,
                metadataSourceY: 0,
                metadataTownMapId: 0,
                metadataObservationSource: TemporaryPortalField.RemoteTownPortalObservationSource.WzReturnMapFallback,
                metadataRecordedAt: 0);

            Assert.True(preferredSourceMapId.HasValue);
            Assert.Equal(101000000, preferredSourceMapId.Value);
        }

        [Fact]
        public void PreferredSourceSelection_UsesMetadata_WhenOwnerObservationsAndExistingSourceAreMissing()
        {
            int? preferredSourceMapId = TemporaryPortalField.ResolvePreferredRemoteTownPortalSourceMapIdForTesting(
                currentMapId: 100000000,
                hasExistingDestination: false,
                existingDestinationMapId: 0,
                existingDestinationX: 0f,
                existingDestinationY: 0f,
                hasMetadata: true,
                metadataSourceMapId: 102000000,
                metadataSourceX: 70f,
                metadataSourceY: 80f,
                metadataTownMapId: 100000000,
                metadataObservationSource: TemporaryPortalField.RemoteTownPortalObservationSource.InferredSourceField,
                metadataRecordedAt: 5);

            Assert.True(preferredSourceMapId.HasValue);
            Assert.Equal(102000000, preferredSourceMapId.Value);
        }

        [Theory]
        [InlineData((int)PortalType.StartPoint, "west00", true)]
        [InlineData(2, "sp", true)]
        [InlineData(2, "north00", false)]
        public void SourceFallbackStartPortalDetection_MatchesStartPortalHints(int portalTypeId, string portalName, bool expected)
        {
            bool isStartPortal = TemporaryPortalField.IsRemoteTownPortalSourceFallbackStartPortalForTesting(portalTypeId, portalName);

            Assert.Equal(expected, isStartPortal);
        }

        [Fact]
        public void MysticDoorLinking_DisabledDuringRemovingPhase()
        {
            bool shouldLink = TemporaryPortalField.ShouldLinkRemoteTownPortalForTesting(
                TemporaryPortalField.RemoteTownPortalVisualPhase.Removing,
                hasDestination: true);

            Assert.False(shouldLink);
        }

        [Fact]
        public void OpenGateLinking_DisabledWhilePartnerOpening()
        {
            bool shouldLink = TemporaryPortalField.ShouldLinkRemoteOpenGatePortalForTesting(
                TemporaryPortalField.RemoteOpenGateVisualPhase.Stable,
                hasPartner: true,
                TemporaryPortalField.RemoteOpenGateVisualPhase.Opening);

            Assert.False(shouldLink);
        }

        [Fact]
        public void OpenGateVisualMode_RemainsSoloUntilPartnerStable()
        {
            TemporaryPortalField.RemoteOpenGateVisualMode mode = TemporaryPortalField.ResolveRemoteOpenGateVisualModeForTesting(
                TemporaryPortalField.RemoteOpenGateVisualPhase.Stable,
                hasPartner: true,
                TemporaryPortalField.RemoteOpenGateVisualPhase.Opening);

            Assert.Equal(TemporaryPortalField.RemoteOpenGateVisualMode.Solo, mode);
        }
    }
}
