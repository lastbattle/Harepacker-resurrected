using HaCreator.MapSimulator.Fields;

namespace UnitTest_MapSimulator
{
    public sealed class RemoteTownPortalParityTests
    {
        [Fact]
        public void ShouldRefreshInferredMetadata_WhenStrongerDifferentSourceObservationWinsActiveDoor()
        {
            bool shouldRefresh = TemporaryPortalField.ShouldRefreshRemoteTownPortalInferredMetadataForTesting(
                hasMetadata: true,
                metadataSourceMapId: 101000000,
                metadataSourceX: 100f,
                metadataSourceY: 200f,
                metadataTownMapId: 100000000,
                metadataObservationSource: TemporaryPortalField.RemoteTownPortalObservationSource.InferredSourceField,
                metadataRecordedAt: 1000,
                hasObservation: true,
                observationSourceMapId: 102000000,
                observationSourceX: 400f,
                observationSourceY: 500f,
                observationTownMapId: 100000000,
                observationSource: TemporaryPortalField.RemoteTownPortalObservationSource.EnterField,
                observationRecordedAt: 2000,
                refreshedDestinationMapId: 102000000,
                refreshedDestinationX: 400f,
                refreshedDestinationY: 500f);

            Assert.True(shouldRefresh);
        }

        [Fact]
        public void ShouldRefreshInferredMetadata_WhenSameSourceApproximationImproves()
        {
            bool shouldRefresh = TemporaryPortalField.ShouldRefreshRemoteTownPortalInferredMetadataForTesting(
                hasMetadata: true,
                metadataSourceMapId: 101000000,
                metadataSourceX: 100f,
                metadataSourceY: 200f,
                metadataTownMapId: 100000000,
                metadataObservationSource: TemporaryPortalField.RemoteTownPortalObservationSource.InferredSourceField,
                metadataRecordedAt: 1000,
                hasObservation: true,
                observationSourceMapId: 101000000,
                observationSourceX: 150f,
                observationSourceY: 260f,
                observationTownMapId: 100000000,
                observationSource: TemporaryPortalField.RemoteTownPortalObservationSource.MovementSnapshot,
                observationRecordedAt: 2500,
                refreshedDestinationMapId: 101000000,
                refreshedDestinationX: 150f,
                refreshedDestinationY: 260f);

            Assert.True(shouldRefresh);
        }

        [Fact]
        public void ShouldNotRefreshInferredMetadata_WhenPacketCastMetadataIsAlreadyAuthoritative()
        {
            bool shouldRefresh = TemporaryPortalField.ShouldRefreshRemoteTownPortalInferredMetadataForTesting(
                hasMetadata: true,
                metadataSourceMapId: 101000000,
                metadataSourceX: 100f,
                metadataSourceY: 200f,
                metadataTownMapId: 100000000,
                metadataObservationSource: TemporaryPortalField.RemoteTownPortalObservationSource.PacketCast,
                metadataRecordedAt: 1000,
                hasObservation: true,
                observationSourceMapId: 101000000,
                observationSourceX: 150f,
                observationSourceY: 260f,
                observationTownMapId: 100000000,
                observationSource: TemporaryPortalField.RemoteTownPortalObservationSource.EnterField,
                observationRecordedAt: 2500,
                refreshedDestinationMapId: 101000000,
                refreshedDestinationX: 150f,
                refreshedDestinationY: 260f);

            Assert.False(shouldRefresh);
        }
    }
}
