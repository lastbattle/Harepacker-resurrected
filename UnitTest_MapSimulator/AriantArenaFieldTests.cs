using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Managers;
using System.IO;
using System.Text;

namespace UnitTest_MapSimulator
{
    public sealed class AriantArenaFieldTests
    {
        [Fact]
        public void TryParsePacketLine_ShowResultAlias_AllowsEmptyPayload()
        {
            bool parsed = AriantArenaPacketInboxManager.TryParsePacketLine(
                "showresult",
                out int packetType,
                out byte[] payload,
                out string error);

            Assert.True(parsed, error);
            Assert.Equal(171, packetType);
            Assert.NotNull(payload);
            Assert.Empty(payload);
        }

        [Fact]
        public void TryParsePacketLine_UserScoreAlias_ParsesHexPayload()
        {
            bool parsed = AriantArenaPacketInboxManager.TryParsePacketLine(
                "userscore 01 04 00 54 65 73 74 2A 00 00 00",
                out int packetType,
                out byte[] payload,
                out string error);

            Assert.True(parsed, error);
            Assert.Equal(354, packetType);
            Assert.Equal(new byte[] { 0x01, 0x04, 0x00, 0x54, 0x65, 0x73, 0x74, 0x2A, 0x00, 0x00, 0x00 }, payload);
        }

        [Fact]
        public void TryApplyPacket_UserScoreBatch_SortsEntriesAndSuppressesHiddenLocalJob()
        {
            AriantArenaField field = new AriantArenaField();
            field.Enable();
            field.SetLocalPlayerState("Local", 900);

            byte[] payload = BuildUserScorePayload(
                ("Local", 10),
                ("Beta", 30),
                ("Alpha", 40));

            bool applied = field.TryApplyPacket(354, payload, currentTimeMs: 0, out string errorMessage);

            Assert.True(applied, errorMessage);
            Assert.Null(errorMessage);
            Assert.Equal(2, field.Entries.Count);
            Assert.Equal("Alpha", field.Entries[0].Name);
            Assert.Equal(40, field.Entries[0].Score);
            Assert.Equal("Beta", field.Entries[1].Name);
            Assert.Equal(30, field.Entries[1].Score);
        }

        private static byte[] BuildUserScorePayload(params (string Name, int Score)[] updates)
        {
            using MemoryStream stream = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)updates.Length);
            foreach ((string name, int score) in updates)
            {
                byte[] nameBytes = Encoding.Default.GetBytes(name);
                writer.Write((ushort)nameBytes.Length);
                writer.Write(nameBytes);
                writer.Write(score);
            }

            writer.Flush();
            return stream.ToArray();
        }
    }
}
