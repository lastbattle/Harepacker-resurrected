using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Pools;
using MapleLib.PacketLib;
using System.Text;

namespace UnitTest_MapSimulator
{
    public class AriantArenaFieldTests
    {
        [Theory]
        [InlineData("hit", 218)]
        [InlineData("upgradetomb", 221)]
        [InlineData("usereffect", 224)]
        [InlineData("grenade", 230)]
        public void AriantInboxPacketAlias_ParsesOfficialPacketType(string alias, int expectedPacketType)
        {
            bool parsed = AriantArenaPacketInboxManager.TryParsePacketLine(
                $"{alias} 00",
                out int packetType,
                out byte[] payload,
                out string error);

            Assert.True(parsed, error);
            Assert.Equal(expectedPacketType, packetType);
            Assert.Single(payload);
            Assert.Equal(0, payload[0]);
        }

        [Theory]
        [InlineData(RemoteUserPacketType.UserHitOfficial, 218)]
        [InlineData(RemoteUserPacketType.UserUpgradeTombOfficial, 221)]
        [InlineData(RemoteUserPacketType.UserEffectOfficial, 224)]
        [InlineData(RemoteUserPacketType.UserThrowGrenadeOfficial, 230)]
        public void AriantOfficialRemoteUserPacketFamily_MapsThroughSharedResolver(
            RemoteUserPacketType sourcePacketType,
            int expectedAriantPacketType)
        {
            bool mapped = MapSimulator.TryResolveAriantArenaOfficialRemoteUserPacketType(
                (int)sourcePacketType,
                out int ariantPacketType);

            Assert.True(mapped);
            Assert.Equal(expectedAriantPacketType, ariantPacketType);
        }

        [Fact]
        public void RemoteUserEffectCodec_ParsesPrefixedMapleStringWithTrailingInt_ForStringEffect()
        {
            byte[] payload = BuildRemoteEffectPayload(
                characterId: 1001,
                effectType: (byte)RemoteUserEffectSubtype.StringEffect,
                writeEffectPayload: writer =>
                {
                    writer.WriteByte(1);
                    writer.WriteMapleString("Effect/AriantArena/Test");
                    writer.WriteInt(77);
                });

            bool parsed = RemoteUserPacketCodec.TryParseEffect(
                payload,
                out RemoteUserEffectPacket packet,
                out string error);

            Assert.True(parsed, error);
            Assert.Equal(RemoteUserEffectSubtype.StringEffect, packet.KnownSubtype);
            Assert.Equal("Effect/AriantArena/Test", packet.StringValue);
            Assert.Equal(77, packet.SecondaryInt32Value);
        }

        [Fact]
        public void RemoteUserEffectCodec_ParsesPrefixedMapleString_ForCarnivalReservedEffect()
        {
            byte[] payload = BuildRemoteEffectPayload(
                characterId: 1002,
                effectType: (byte)RemoteUserEffectSubtype.CarnivalReservedEffect,
                writeEffectPayload: writer =>
                {
                    writer.WriteByte(2);
                    writer.WriteMapleString("Effect/AriantArena/Reserved");
                });

            bool parsed = RemoteUserPacketCodec.TryParseEffect(
                payload,
                out RemoteUserEffectPacket packet,
                out string error);

            Assert.True(parsed, error);
            Assert.Equal(RemoteUserEffectSubtype.CarnivalReservedEffect, packet.KnownSubtype);
            Assert.Equal("Effect/AriantArena/Reserved", packet.StringValue);
            Assert.Null(packet.SecondaryInt32Value);
        }

        [Fact]
        public void RemoteUserEffectCodec_ParsesPlainTextWithTrailingInt_ForStringEffect()
        {
            byte[] payload = BuildRemoteEffectPayload(
                characterId: 1003,
                effectType: (byte)RemoteUserEffectSubtype.StringEffect,
                writeEffectPayload: writer =>
                {
                    writer.WriteBytes(Encoding.ASCII.GetBytes("Effect/AriantArena/Plain"));
                    writer.WriteInt(99);
                });

            bool parsed = RemoteUserPacketCodec.TryParseEffect(
                payload,
                out RemoteUserEffectPacket packet,
                out string error);

            Assert.True(parsed, error);
            Assert.Equal("Effect/AriantArena/Plain", packet.StringValue);
            Assert.Equal(99, packet.SecondaryInt32Value);
        }

        [Fact]
        public void RemoteUserHitCodec_ParsesPrefixedMapleStringTail()
        {
            PacketWriter writer = new();
            writer.WriteInt(2001);
            writer.WriteByte(unchecked((byte)-2));
            writer.WriteInt(10);
            writer.WriteInt(0);
            writer.WriteByte(1);
            writer.WriteMapleString("Mob/9999999/Attack1/hit");

            bool parsed = RemoteUserPacketCodec.TryParseHit(
                writer.ToArray(),
                out RemoteUserHitPacket packet,
                out string error);

            Assert.True(parsed, error);
            Assert.Equal("Mob/9999999/Attack1/hit", packet.HitString);
            Assert.NotNull(packet.RawTrailingPayload);
            Assert.NotEmpty(packet.RawTrailingPayload);
        }

        private static byte[] BuildRemoteEffectPayload(
            int characterId,
            byte effectType,
            Action<PacketWriter> writeEffectPayload)
        {
            PacketWriter writer = new();
            writer.WriteInt(characterId);
            writer.WriteByte(effectType);
            writeEffectPayload?.Invoke(writer);
            return writer.ToArray();
        }
    }
}
