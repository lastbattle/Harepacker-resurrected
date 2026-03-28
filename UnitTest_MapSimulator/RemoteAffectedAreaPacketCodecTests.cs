using HaCreator.MapSimulator.Pools;
using Microsoft.Xna.Framework;

namespace UnitTest_MapSimulator
{
    public sealed class RemoteAffectedAreaPacketCodecTests
    {
        [Fact]
        public void TryParseCreated_DecodesExpectedFields()
        {
            byte[] payload = RemoteAffectedAreaPacketCodec.BuildCreatePayload(77, 2, 0x12345678u, 2111003, 17, 4, new Rectangle(10, 20, 90, 120), 5, 9);

            bool parsed = RemoteAffectedAreaPacketCodec.TryParseCreated(payload, out RemoteAffectedAreaCreatedPacket packet, out string error);

            Assert.True(parsed, error);
            Assert.Equal(77, packet.ObjectId);
            Assert.Equal(2, packet.Type);
            Assert.Equal(0x12345678u, packet.OwnerCharacterId);
            Assert.Equal(2111003, packet.SkillId);
            Assert.Equal((byte)17, packet.SkillLevel);
            Assert.Equal((short)4, packet.StartDelayUnits);
            Assert.Equal(new Rectangle(10, 20, 90, 120), packet.Bounds);
            Assert.Equal(5, packet.ElementAttribute);
            Assert.Equal(9, packet.Phase);
        }

        [Fact]
        public void TryParseCreated_NormalizesInvertedBounds()
        {
            byte[] payload = RemoteAffectedAreaPacketCodec.BuildCreatePayload(5, 1, 99, 125, 1, 0, new Rectangle(40, 60, 1, 1), 0, 0);
            BitConverter.TryWriteBytes(payload.AsSpan(19, 4), 80);
            BitConverter.TryWriteBytes(payload.AsSpan(23, 4), 140);
            BitConverter.TryWriteBytes(payload.AsSpan(27, 4), 20);
            BitConverter.TryWriteBytes(payload.AsSpan(31, 4), 40);

            bool parsed = RemoteAffectedAreaPacketCodec.TryParseCreated(payload, out RemoteAffectedAreaCreatedPacket packet, out string error);

            Assert.True(parsed, error);
            Assert.Equal(new Rectangle(20, 40, 60, 100), packet.Bounds);
        }

        [Fact]
        public void TryParseRemoved_DecodesExpectedFields()
        {
            byte[] payload = RemoteAffectedAreaPacketCodec.BuildRemovePayload(4123);

            bool parsed = RemoteAffectedAreaPacketCodec.TryParseRemoved(payload, out RemoteAffectedAreaRemovedPacket packet, out string error);

            Assert.True(parsed, error);
            Assert.Equal(4123, packet.ObjectId);
        }

        [Fact]
        public void TryParseCreated_RejectsUnexpectedLength()
        {
            bool parsed = RemoteAffectedAreaPacketCodec.TryParseCreated(new byte[] { 1, 2, 3 }, out _, out string error);

            Assert.False(parsed);
            Assert.Equal("AffectedAreaCreate expects 43 bytes but received 3.", error);
        }
    }
}
