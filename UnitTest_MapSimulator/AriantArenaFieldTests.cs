using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Character;
using Microsoft.Xna.Framework;
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

        [Fact]
        public void RemoteParticipant_AddMoveRemove_UpdatesFieldState()
        {
            AriantArenaField field = new AriantArenaField();
            field.Enable();

            CharacterBuild build = new CharacterBuild
            {
                Name = "RemoteOne"
            };

            field.UpsertRemoteParticipant(build, new Vector2(100, 200), facingRight: false, actionName: "walk1");

            Assert.Equal(1, field.RemoteParticipantCount);
            Assert.True(field.TryGetRemoteParticipant("RemoteOne", out AriantArenaRemoteParticipantSnapshot snapshot));
            Assert.Equal(new Vector2(100, 200), snapshot.Position);
            Assert.False(snapshot.FacingRight);
            Assert.Equal("walk1", snapshot.ActionName);

            bool moved = field.TryMoveRemoteParticipant("RemoteOne", new Vector2(120, 210), facingRight: true, actionName: "stand1", out string moveError);

            Assert.True(moved, moveError);
            Assert.True(field.TryGetRemoteParticipant("RemoteOne", out snapshot));
            Assert.Equal(new Vector2(120, 210), snapshot.Position);
            Assert.True(snapshot.FacingRight);
            Assert.Equal("stand1", snapshot.ActionName);

            Assert.True(field.RemoveRemoteParticipant("RemoteOne"));
            Assert.Equal(0, field.RemoteParticipantCount);
        }

        [Fact]
        public void TryMoveRemoteParticipant_MissingActor_ReturnsError()
        {
            AriantArenaField field = new AriantArenaField();
            field.Enable();

            bool moved = field.TryMoveRemoteParticipant("Missing", new Vector2(10, 10), facingRight: null, actionName: null, out string error);

            Assert.False(moved);
            Assert.Equal("Remote Ariant actor 'Missing' does not exist.", error);
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
