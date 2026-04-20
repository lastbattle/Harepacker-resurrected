using HaCreator.MapSimulator.Fields;

namespace UnitTest_MapSimulator
{
    public class RemotePortalPoolParityTests
    {
        [Fact]
        public void CanRememberPacketCastMetadataFromResolvedFallback_WhenSourceMapResolvesToDestinationTown_ReturnsTrue()
        {
            bool shouldRemember = TemporaryPortalField.CanRememberRemoteTownPortalPacketCastMetadataFromResolvedFallbackForTesting(
                currentMapId: 100020000,
                resolvedDestinationMapId: 100000000,
                currentMapHasTownPortalPoint: false,
                hasCurrentMapReturnTownMap: true,
                currentMapReturnTownMapId: 100000000);

            Assert.True(shouldRemember);
        }

        [Fact]
        public void CanRememberPacketCastMetadataFromResolvedFallback_WhenCurrentMapHasTownPortalPoint_ReturnsFalse()
        {
            bool shouldRemember = TemporaryPortalField.CanRememberRemoteTownPortalPacketCastMetadataFromResolvedFallbackForTesting(
                currentMapId: 100000000,
                resolvedDestinationMapId: 100020000,
                currentMapHasTownPortalPoint: true,
                hasCurrentMapReturnTownMap: true,
                currentMapReturnTownMapId: 100000000);

            Assert.False(shouldRemember);
        }

        [Fact]
        public void CanRememberPacketCastMetadataFromResolvedFallback_WhenReturnTownMapIsUnavailable_ReturnsFalse()
        {
            bool shouldRemember = TemporaryPortalField.CanRememberRemoteTownPortalPacketCastMetadataFromResolvedFallbackForTesting(
                currentMapId: 100020000,
                resolvedDestinationMapId: 100000000,
                currentMapHasTownPortalPoint: false,
                hasCurrentMapReturnTownMap: false,
                currentMapReturnTownMapId: -1);

            Assert.False(shouldRemember);
        }

        [Fact]
        public void CanRememberPacketCastMetadataFromResolvedFallback_WhenResolvedDestinationDoesNotMatchReturnTownMap_ReturnsFalse()
        {
            bool shouldRemember = TemporaryPortalField.CanRememberRemoteTownPortalPacketCastMetadataFromResolvedFallbackForTesting(
                currentMapId: 100020000,
                resolvedDestinationMapId: 100000000,
                currentMapHasTownPortalPoint: false,
                hasCurrentMapReturnTownMap: true,
                currentMapReturnTownMapId: 200000000);

            Assert.False(shouldRemember);
        }
    }
}
