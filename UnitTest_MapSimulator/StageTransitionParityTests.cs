using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;

namespace UnitTest_MapSimulator
{
    public sealed class StageTransitionParityTests
    {
        [Fact]
        public void TryDecodeOfficialSetFieldPayload_DecodesCharacterDataBranchTransferHeadAndTrailingBytes()
        {
            byte[] payload = PacketStageTransitionRuntime.BuildCharacterDataSetFieldPayload(
                mapId: 910000000,
                portalIndex: 2,
                hp: 3456,
                channelId: 4,
                oldDriverId: 77,
                fieldKey: 9,
                characterId: 123456,
                characterName: "Parity",
                linkedCharacterName: "Linked",
                characterDataTail: new byte[] { 0xAA, 0xBB, 0xCC },
                serverFileTime: 0x0102030405060708L);

            bool decoded = PacketStageTransitionRuntime.TryDecodeOfficialSetFieldPayload(
                payload,
                out PacketSetFieldPacket packet,
                out string? error);

            Assert.True(decoded, error);
            Assert.Null(error);
            Assert.True(packet.HasCharacterData);
            Assert.True(packet.SupportsFieldTransfer);
            Assert.Equal(4, packet.ChannelId);
            Assert.Equal(77, packet.OldDriverId);
            Assert.Equal((byte)9, packet.FieldKey);
            Assert.Equal(910000000, packet.FieldId);
            Assert.Equal((byte)2, packet.PortalIndex);
            Assert.Equal(3456, packet.Hp);
            Assert.Equal(3, packet.TrailingBytes);
            Assert.Equal(0x0102030405060708L, packet.ServerFileTime);
        }

        [Fact]
        public void TryDecodeOpcodeFramedPacket_DecodesSetFieldRelayPayloadFromLoginBridgeTraffic()
        {
            byte[] payload = PacketStageTransitionRuntime.BuildOfficialSetFieldPayload(
                mapId: 100000000,
                portalIndex: 1,
                channelId: 2,
                fieldKey: 5);
            byte[] framedPacket = new byte[sizeof(ushort) + payload.Length];
            BitConverter.GetBytes((ushort)LoginPacketType.SetField).CopyTo(framedPacket, 0);
            payload.CopyTo(framedPacket, sizeof(ushort));

            bool decoded = LoginPacketInboxManager.TryDecodeOpcodeFramedPacket(
                framedPacket,
                out LoginPacketType packetType,
                out string[] arguments);

            Assert.True(decoded);
            Assert.Equal(LoginPacketType.SetField, packetType);
            Assert.Single(arguments);
            Assert.Equal($"payloadhex={Convert.ToHexString(payload)}", arguments[0]);
        }

        [Theory]
        [InlineData("field", 141)]
        [InlineData("setitc", 142)]
        [InlineData("cashshop", 143)]
        [InlineData("backeffect", 144)]
        [InlineData("objectvisible", 145)]
        [InlineData("clearbackeffect", 146)]
        public void StageTransitionPacketInboxManager_TryParseLine_ParsesNamedStagePackets(string token, int expectedPacketType)
        {
            string line = expectedPacketType is 142 or 143 or 146
                ? token
                : $"{token} payloadhex=00";

            bool parsed = StageTransitionPacketInboxManager.TryParseLine(
                line,
                out StageTransitionPacketInboxMessage? message,
                out string? error);

            Assert.True(parsed, error);
            Assert.NotNull(message);
            Assert.Equal(expectedPacketType, message!.PacketType);
        }
    }
}
