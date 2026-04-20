using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Interaction;

namespace UnitTest_MapSimulator
{
    public class PacketOwnedQuestStartRequestTests
    {
        [Fact]
        public void ResolveIncludeUserPosition_AutoAlertQuest_OmitsCoordinates()
        {
            bool includeUserPosition = PacketOwnedQuestStartRequest.ResolveIncludeUserPosition(isAutoAlertQuest: true);
            Assert.False(includeUserPosition);
        }

        [Fact]
        public void ResolveIncludeUserPosition_NonAutoAlertQuest_IncludesCoordinates()
        {
            bool includeUserPosition = PacketOwnedQuestStartRequest.ResolveIncludeUserPosition(isAutoAlertQuest: false);
            Assert.True(includeUserPosition);
        }

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(true, false, true)]
        [InlineData(false, true, true)]
        [InlineData(true, true, true)]
        public void ResolveIsAutoCompletionAlertQuest_UsesQuestInfoCandidateFlags(
            bool autoComplete,
            bool autoPreComplete,
            bool expected)
        {
            bool isCandidate = PacketOwnedQuestStartRequest.ResolveIsAutoCompletionAlertQuest(
                autoComplete,
                autoPreComplete);
            Assert.Equal(expected, isCandidate);
        }

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(true, false, true)]
        [InlineData(false, true, true)]
        [InlineData(true, true, true)]
        public void ResolveIsAutoAlertQuest_MatchesAutoStartOrAutoCompletionAlert(
            bool isAutoStartQuest,
            bool isAutoCompletionAlertQuest,
            bool expected)
        {
            bool isAutoAlertQuest = PacketOwnedQuestStartRequest.ResolveIsAutoAlertQuest(
                isAutoStartQuest,
                isAutoCompletionAlertQuest);
            Assert.Equal(expected, isAutoAlertQuest);
        }

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(true, false, false)]
        [InlineData(false, true, false)]
        [InlineData(true, true, true)]
        public void ResolveShouldRegisterAutoCompletionAlertQuest_RequiresCandidateAndOutstandingDemand(
            bool isCandidate,
            bool hasCompletionDemandOutstanding,
            bool expected)
        {
            bool shouldRegister = PacketOwnedQuestStartRequest.ResolveShouldRegisterAutoCompletionAlertQuest(
                isCandidate,
                hasCompletionDemandOutstanding);
            Assert.Equal(expected, shouldRegister);
        }

        [Fact]
        public void Create_WithAutoAlertGateDisabled_OmitsUserCoordinatesFromPayload()
        {
            PacketOwnedQuestStartRequest request = PacketOwnedQuestStartRequest.Create(
                questId: 10015,
                npcTemplateId: 9000036,
                deliveryItemPosition: 4,
                userX: 50,
                userY: -42,
                includeUserPosition: false);

            bool decoded = PacketOwnedQuestStartRequest.TryDecodePayload(
                request.Payload,
                out int requestKind,
                out int questId,
                out int npcTemplateId,
                out int deliveryItemPosition,
                out short userX,
                out short userY,
                out bool includesUserPosition,
                out string error);

            Assert.True(decoded, error);
            Assert.Equal(PacketOwnedQuestStartRequest.StartRequestKind, requestKind);
            Assert.Equal(10015, questId);
            Assert.Equal(9000036, npcTemplateId);
            Assert.Equal(4, deliveryItemPosition);
            Assert.False(includesUserPosition);
            Assert.Equal(0, userX);
            Assert.Equal(0, userY);
        }

        [Fact]
        public void IsPacketOwnedQuestResultStartQuestRequestBlocked_BlocksWhileRequestSent()
        {
            bool blocked = MapSimulator.IsPacketOwnedQuestResultStartQuestRequestBlocked(
                requestSent: true,
                requestSentTick: 1000,
                sharedUtilityRequestTick: int.MinValue,
                currentTick: 4000,
                cooldownMs: 500);

            Assert.True(blocked);
        }

        [Fact]
        public void IsPacketOwnedQuestResultStartQuestRequestBlocked_BlocksFromSharedCooldown()
        {
            bool blocked = MapSimulator.IsPacketOwnedQuestResultStartQuestRequestBlocked(
                requestSent: false,
                requestSentTick: int.MinValue,
                sharedUtilityRequestTick: 1000,
                currentTick: 1200,
                cooldownMs: 500);

            Assert.True(blocked);
        }

        [Fact]
        public void IsPacketOwnedQuestResultStartQuestRequestBlocked_AllowsWhenNoSentFlagAndCooldownElapsed()
        {
            bool blocked = MapSimulator.IsPacketOwnedQuestResultStartQuestRequestBlocked(
                requestSent: false,
                requestSentTick: 1000,
                sharedUtilityRequestTick: 1000,
                currentTick: 1700,
                cooldownMs: 500);

            Assert.False(blocked);
        }

        [Fact]
        public void ShouldConsumePacketOwnedQuestResultStartQuestExclusiveReset_RequiresResetMarker()
        {
            byte[] payloadWithReset = { 1, 0 };
            byte[] payloadWithoutReset = { 0, 0 };

            bool consumedWithReset =
                MapSimulator.ShouldConsumePacketOwnedQuestResultStartQuestExclusiveResetFromInventoryOperationPayload(
                    payloadWithReset);
            bool consumedWithoutReset =
                MapSimulator.ShouldConsumePacketOwnedQuestResultStartQuestExclusiveResetFromInventoryOperationPayload(
                    payloadWithoutReset);

            Assert.True(consumedWithReset);
            Assert.False(consumedWithoutReset);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(false, false)]
        public void ShouldClearPacketOwnedQuestResultStartQuestRequestLatchFromSharedExclusiveReset_MirrorsSentOwnership(
            bool requestSent,
            bool expected)
        {
            bool shouldClear = MapSimulator
                .ShouldClearPacketOwnedQuestResultStartQuestRequestLatchFromSharedExclusiveReset(requestSent);

            Assert.Equal(expected, shouldClear);
        }
    }
}
