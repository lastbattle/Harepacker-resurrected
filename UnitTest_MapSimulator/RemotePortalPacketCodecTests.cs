using HaCreator.MapSimulator.Fields;

namespace UnitTest_MapSimulator
{
    public sealed class RemotePortalPacketCodecTests
    {
        [Fact]
        public void TryParseTownPortalCreated_DecodesExpectedFields()
        {
            byte[] payload = { 1, 0x78, 0x56, 0x34, 0x12, 0x10, 0x00, 0x20, 0x00 };

            bool parsed = RemotePortalPacketCodec.TryParseTownPortalCreated(payload, out RemoteTownPortalCreatedPacket packet, out string error);

            Assert.True(parsed, error);
            Assert.Equal((byte)1, packet.State);
            Assert.Equal(0x12345678u, packet.OwnerCharacterId);
            Assert.Equal((short)16, packet.X);
            Assert.Equal((short)32, packet.Y);
        }

        [Fact]
        public void TryParseTownPortalRemoved_DecodesExpectedFields()
        {
            byte[] payload = { 0, 0x04, 0x03, 0x02, 0x01 };

            bool parsed = RemotePortalPacketCodec.TryParseTownPortalRemoved(payload, out RemoteTownPortalRemovedPacket packet, out string error);

            Assert.True(parsed, error);
            Assert.Equal((byte)0, packet.State);
            Assert.Equal(0x01020304u, packet.OwnerCharacterId);
        }

        [Fact]
        public void TryParseOpenGateCreated_DecodesExpectedFields()
        {
            byte[] payload = { 1, 0x44, 0x33, 0x22, 0x11, 0xF6, 0xFF, 0x2A, 0x00, 1, 0x88, 0x77, 0x66, 0x55 };

            bool parsed = RemotePortalPacketCodec.TryParseOpenGateCreated(payload, out RemoteOpenGateCreatedPacket packet, out string error);

            Assert.True(parsed, error);
            Assert.Equal((byte)1, packet.State);
            Assert.Equal(0x11223344u, packet.OwnerCharacterId);
            Assert.Equal((short)-10, packet.X);
            Assert.Equal((short)42, packet.Y);
            Assert.True(packet.IsFirstSlot);
            Assert.Equal(0x55667788u, packet.PartyId);
        }

        [Fact]
        public void TryParseOpenGateRemoved_DecodesExpectedFields()
        {
            byte[] payload = { 1, 0xDD, 0xCC, 0xBB, 0xAA, 0 };

            bool parsed = RemotePortalPacketCodec.TryParseOpenGateRemoved(payload, out RemoteOpenGateRemovedPacket packet, out string error);

            Assert.True(parsed, error);
            Assert.Equal((byte)1, packet.State);
            Assert.Equal(0xAABBCCDDu, packet.OwnerCharacterId);
            Assert.False(packet.IsFirstSlot);
        }

        [Fact]
        public void TryParseTownPortalCreated_RejectsUnexpectedLength()
        {
            byte[] payload = { 1, 2, 3 };

            bool parsed = RemotePortalPacketCodec.TryParseTownPortalCreated(payload, out _, out string error);

            Assert.False(parsed);
            Assert.Equal("TownPortalCreate expects 9 bytes but received 3.", error);
        }
    }
}
