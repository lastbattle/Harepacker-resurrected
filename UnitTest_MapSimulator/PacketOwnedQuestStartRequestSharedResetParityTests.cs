using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Interaction;

namespace UnitTest_MapSimulator
{
    public class PacketOwnedQuestStartRequestSharedResetParityTests
    {
        [Fact]
        public void SharedExclusiveResetClearsFollowUpLatchOnlyWhenSent()
        {
            Assert.True(MapSimulator.ShouldClearPacketOwnedQuestResultStartQuestRequestLatchFromSharedExclusiveReset(requestSent: true));
            Assert.False(MapSimulator.ShouldClearPacketOwnedQuestResultStartQuestRequestLatchFromSharedExclusiveReset(requestSent: false));
        }

        [Fact]
        public void InventoryOperationResetMarkerDetectionFollowsClientShape()
        {
            byte[] validResetPayload =
            {
                1, // reset marker
                1, // operation count
                0, // op type
                0, // inventory type
                0, 0 // position
            };

            byte[] invalidResetPayload =
            {
                2, // invalid reset marker
                0
            };

            Assert.True(MapSimulator.ShouldConsumePacketOwnedQuestResultStartQuestExclusiveResetFromInventoryOperationPayload(validResetPayload));
            Assert.False(MapSimulator.ShouldConsumePacketOwnedQuestResultStartQuestExclusiveResetFromInventoryOperationPayload(invalidResetPayload));
        }

        [Fact]
        public void AutoAlertQuestOmitsSubtype10FollowUpUserPosition()
        {
            Assert.True(PacketOwnedQuestStartRequest.ResolveIncludeUserPosition(isAutoAlertQuest: false));
            Assert.False(PacketOwnedQuestStartRequest.ResolveIncludeUserPosition(isAutoAlertQuest: true));
        }

        [Fact]
        public void FollowUpRequestBlockMatchesSentAndSharedCooldownOwnership()
        {
            Assert.True(MapSimulator.IsPacketOwnedQuestResultStartQuestRequestBlocked(
                requestSent: true,
                requestSentTick: int.MinValue,
                sharedUtilityRequestTick: int.MinValue,
                currentTick: 1000,
                cooldownMs: 500));

            Assert.True(MapSimulator.IsPacketOwnedQuestResultStartQuestRequestBlocked(
                requestSent: false,
                requestSentTick: int.MinValue,
                sharedUtilityRequestTick: 900,
                currentTick: 1000,
                cooldownMs: 500));

            Assert.False(MapSimulator.IsPacketOwnedQuestResultStartQuestRequestBlocked(
                requestSent: false,
                requestSentTick: int.MinValue,
                sharedUtilityRequestTick: int.MinValue,
                currentTick: 1000,
                cooldownMs: 500));
        }
    }
}
