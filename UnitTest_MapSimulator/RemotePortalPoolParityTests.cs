using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Fields;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace UnitTest_MapSimulator
{
    public class RemotePortalPoolParityTests
    {
        [Fact]
        public void ResolveRemoteTownPortalCreatePhase_NewOwnerStateZero_UsesOpeningPhase()
        {
            TemporaryPortalField.RemoteTownPortalVisualPhase phase =
                TemporaryPortalField.ResolveRemoteTownPortalCreatePhaseForTesting(
                    packetState: 0,
                    hasExistingState: false,
                    existingPhase: TemporaryPortalField.RemoteTownPortalVisualPhase.Stable);

            Assert.Equal(TemporaryPortalField.RemoteTownPortalVisualPhase.Opening, phase);
        }

        [Fact]
        public void ResolveRemoteTownPortalCreatePhase_ActiveOwnerStateZero_StaysStable()
        {
            TemporaryPortalField.RemoteTownPortalVisualPhase phase =
                TemporaryPortalField.ResolveRemoteTownPortalCreatePhaseForTesting(
                    packetState: 0,
                    hasExistingState: true,
                    existingPhase: TemporaryPortalField.RemoteTownPortalVisualPhase.Stable);

            Assert.Equal(TemporaryPortalField.RemoteTownPortalVisualPhase.Stable, phase);
        }

        [Fact]
        public void ChooseRemoteTownPortalDestination_PrefersExactPacketCastOverLaterMovementSnapshot()
        {
            TemporaryPortalField.RemoteTownPortalResolvedDestination? destination =
                TemporaryPortalField.ChooseRemoteTownPortalResolvedDestinationForTesting(
                    currentMapId: 100000000,
                    hasMetadata: true,
                    metadataSourceMapId: 101000000,
                    metadataSourceX: 321f,
                    metadataSourceY: 654f,
                    metadataTownMapId: 100000000,
                    metadataObservationSource: TemporaryPortalField.RemoteTownPortalObservationSource.PacketCast,
                    metadataRecordedAt: 100,
                    hasObservation: true,
                    observationSourceMapId: 101000000,
                    observationSourceX: 111f,
                    observationSourceY: 222f,
                    observationTownMapId: 100000000,
                    observationSource: TemporaryPortalField.RemoteTownPortalObservationSource.MovementSnapshot,
                    observationRecordedAt: 200);

            Assert.True(destination.HasValue);
            Assert.Equal(101000000, destination.Value.MapId);
            Assert.Equal(321f, destination.Value.X);
            Assert.Equal(654f, destination.Value.Y);
        }

        [Fact]
        public void ShouldReplaceRemoteTownPortalOwnerObservation_SameSourceNewerApproximation_ReplacesOlderSnapshot()
        {
            bool shouldReplace = TemporaryPortalField.ShouldReplaceRemoteTownPortalOwnerObservationForTesting(
                existingSourceMapId: 101000000,
                existingTownMapId: 100000000,
                existingObservationSource: TemporaryPortalField.RemoteTownPortalObservationSource.MovementSnapshot,
                existingRecordedAt: 100,
                sourceMapId: 101000000,
                townMapId: 100000000,
                newObservationSource: TemporaryPortalField.RemoteTownPortalObservationSource.MovementSnapshot,
                newRecordedAt: 200);

            Assert.True(shouldReplace);
        }

        [Fact]
        public void ResolveRemoteTownPortalDestinationFromCachedState_UsesApproximationWhenNoPreciseSourceExists()
        {
            TemporaryPortalField.RemoteTownPortalResolvedDestination? destination =
                TemporaryPortalField.ResolveRemoteTownPortalDestinationFromCachedStateForTesting(
                    currentMapId: 100000000,
                    hasIncomingDestination: false,
                    incomingDestinationMapId: 0,
                    incomingDestinationX: 0,
                    incomingDestinationY: 0,
                    hasExistingDestination: false,
                    existingDestinationMapId: 0,
                    existingDestinationX: 0,
                    existingDestinationY: 0,
                    hasMetadata: false,
                    metadataSourceMapId: 0,
                    metadataSourceX: 0,
                    metadataSourceY: 0,
                    metadataTownMapId: 0,
                    metadataObservationSource: TemporaryPortalField.RemoteTownPortalObservationSource.MovementSnapshot,
                    metadataRecordedAt: 0,
                    hasObservation: true,
                    observationSourceMapId: 101000000,
                    observationSourceX: 77f,
                    observationSourceY: 88f,
                    observationTownMapId: 100000000,
                    observationSource: TemporaryPortalField.RemoteTownPortalObservationSource.MovementSnapshot,
                    observationRecordedAt: 50);

            Assert.True(destination.HasValue);
            Assert.Equal(101000000, destination.Value.MapId);
            Assert.Equal(77f, destination.Value.X);
            Assert.Equal(88f, destination.Value.Y);
        }

        [Fact]
        public void ResolveRemoteTownPortalMoveObservationPosition_UsesLiveActorPositionOverPassiveSnapshot()
        {
            PlayerMovementSyncSnapshot snapshot = CreateSnapshot(10, 20);

            Vector2 position = MapSimulator.ResolveRemoteTownPortalMoveObservationPositionForTesting(
                snapshot,
                new Vector2(30f, 40f));

            Assert.Equal(new Vector2(30f, 40f), position);
        }

        [Fact]
        public void ResolveRemoteTownPortalMoveObservationPosition_FallsBackToPassiveSnapshotWhenLivePositionMissing()
        {
            PlayerMovementSyncSnapshot snapshot = CreateSnapshot(10, 20);

            Vector2 position = MapSimulator.ResolveRemoteTownPortalMoveObservationPositionForTesting(
                snapshot,
                liveActorPosition: null);

            Assert.Equal(new Vector2(10f, 20f), position);
        }

        [Fact]
        public void ResolveRemoteOpenGateVisualMode_WithPartnerStable_UsesLinkedMode()
        {
            TemporaryPortalField.RemoteOpenGateVisualMode mode =
                TemporaryPortalField.ResolveRemoteOpenGateVisualModeForTesting(
                    TemporaryPortalField.RemoteOpenGateVisualPhase.Stable,
                    hasPartner: true);

            Assert.Equal(TemporaryPortalField.RemoteOpenGateVisualMode.Linked, mode);
        }

        private static PlayerMovementSyncSnapshot CreateSnapshot(int x, int y)
        {
            return new PlayerMovementSyncSnapshot(
                new PassivePositionSnapshot
                {
                    X = x,
                    Y = y,
                    VelocityX = 0,
                    VelocityY = 0,
                    Action = 0,
                    FootholdId = 0,
                    TimeStamp = 0,
                    FacingRight = true
                },
                new List<MovePathElement>());
        }
    }
}
