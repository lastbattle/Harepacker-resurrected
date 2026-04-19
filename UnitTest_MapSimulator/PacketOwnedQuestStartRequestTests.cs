using HaCreator.MapSimulator.Interaction;
using Xunit;

namespace UnitTest_MapSimulator
{
    public sealed class PacketOwnedQuestStartRequestTests
    {
        [Fact]
        public void Create_EncodesClientShapePayloadWithPosition()
        {
            PacketOwnedQuestStartRequest request = PacketOwnedQuestStartRequest.Create(
                questId: 1001,
                npcTemplateId: 1012000,
                deliveryItemPosition: 0,
                userX: 123,
                userY: -45,
                includeUserPosition: true);

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

            Assert.True(decoded);
            Assert.Null(error);
            Assert.Equal(PacketOwnedQuestStartRequest.ClientOpcode, request.Opcode);
            Assert.Equal(PacketOwnedQuestStartRequest.StartRequestKind, requestKind);
            Assert.Equal(1001, questId);
            Assert.Equal(1012000, npcTemplateId);
            Assert.Equal(0, deliveryItemPosition);
            Assert.Equal(123, userX);
            Assert.Equal(-45, userY);
            Assert.True(includesUserPosition);
        }

        [Fact]
        public void TryDecodePayload_RejectsInvalidLength()
        {
            bool decoded = PacketOwnedQuestStartRequest.TryDecodePayload(
                new byte[] { 1, 0, 0 },
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out string error);

            Assert.False(decoded);
            Assert.Contains("must be", error);
        }
    }
}
