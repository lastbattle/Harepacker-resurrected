using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace UnitTest_MapSimulator
{
    public class MemoryGameFieldPacketTests
    {
        [Fact]
        public void TryDispatchMiniRoomPacket_EnterPacketAddsVisitorToMiniRoomShell()
        {
            MemoryGameField field = CreateFieldWithRuntime(out SocialRoomRuntime runtime);

            bool handled = field.TryDispatchMiniRoomPacket(
                BuildMiniRoomEnterPacket(2, "Watcher", 200, CreateLook(faceId: 20000, hairId: 30000)),
                tickCount: 1000,
                out string message);

            Assert.True(handled, message);
            Assert.Contains(runtime.Occupants, occupant => occupant.Name == "Watcher" && occupant.Role == SocialRoomOccupantRole.Visitor);
            Assert.Contains("Job 200", runtime.Occupants[2].Detail);
            Assert.Contains("Face 20000", runtime.Occupants[2].Detail);
            Assert.Contains("Watcher entered", runtime.ChatEntries[^1].Text);
        }

        [Fact]
        public void TryDispatchMiniRoomPacket_ChatPacketAddsSpeakerMessage()
        {
            MemoryGameField field = CreateFieldWithRuntime(out SocialRoomRuntime runtime);

            bool handled = field.TryDispatchMiniRoomPacket(
                BuildMiniRoomChatPacket(1, "Opponent : Ready when you are."),
                tickCount: 1000,
                out string message);

            Assert.True(handled, message);
            Assert.Equal("Opponent : Ready when you are.", runtime.ChatEntries[^1].Text);
        }

        [Fact]
        public void TryDispatchMiniRoomPacket_AvatarPacketRefreshesParticipantDetail()
        {
            MemoryGameField field = CreateFieldWithRuntime(out SocialRoomRuntime runtime);

            Assert.True(field.TryDispatchMiniRoomPacket(
                BuildMiniRoomEnterPacket(1, "Opponent", 211, CreateLook(faceId: 20000, hairId: 30000)),
                tickCount: 1000,
                out _));

            bool handled = field.TryDispatchMiniRoomPacket(
                BuildMiniRoomAvatarPacket(1, CreateLook(faceId: 20010, hairId: 30010)),
                tickCount: 1200,
                out string message);

            Assert.True(handled, message);
            Assert.Contains("Face 20010", runtime.Occupants[1].Detail);
            Assert.Contains("Hair 30010", runtime.Occupants[1].Detail);
            Assert.Contains("updated their MiniRoom avatar", runtime.ChatEntries[^1].Text);
        }

        [Fact]
        public void TryDispatchMiniRoomPacket_VisitorLeaveDoesNotResetBoard()
        {
            MemoryGameField field = CreateFieldWithRuntime(out SocialRoomRuntime runtime);
            field.OpenRoom("Match Cards", "Player", "Opponent");

            Assert.True(field.TryDispatchMiniRoomPacket(
                BuildMiniRoomEnterPacket(2, "Watcher", 200, CreateLook(faceId: 20000, hairId: 30000)),
                tickCount: 1000,
                out _));

            bool handled = field.TryDispatchMiniRoomPacket(
                BuildMiniRoomLeavePacket(2),
                tickCount: 1200,
                out string message);

            Assert.True(handled, message);
            Assert.Equal(MemoryGameField.RoomStage.Lobby, field.Stage);
            Assert.DoesNotContain(runtime.Occupants, occupant => occupant.Name == "Watcher");
        }

        private static MemoryGameField CreateFieldWithRuntime(out SocialRoomRuntime runtime)
        {
            MemoryGameField field = new();
            runtime = SocialRoomRuntime.CreateMiniRoomSample();
            field.AttachMiniRoomRuntime(runtime);
            return field;
        }

        private static LoginAvatarLook CreateLook(int faceId, int hairId)
        {
            return LoginAvatarLookCodec.CreateLook(
                CharacterGender.Male,
                SkinColor.Light,
                faceId,
                hairId,
                new List<KeyValuePair<EquipSlot, int>>());
        }

        private static byte[] BuildMiniRoomEnterPacket(int slot, string name, short jobCode, LoginAvatarLook look)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream);
            writer.Write((byte)4);
            writer.Write((byte)slot);
            writer.Write(LoginAvatarLookCodec.Encode(look));
            WriteMapleString(writer, name);
            writer.Write(jobCode);
            return stream.ToArray();
        }

        private static byte[] BuildMiniRoomAvatarPacket(int slot, LoginAvatarLook look)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream);
            writer.Write((byte)9);
            writer.Write((byte)slot);
            writer.Write(LoginAvatarLookCodec.Encode(look));
            return stream.ToArray();
        }

        private static byte[] BuildMiniRoomChatPacket(int speakerSlot, string text)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream);
            writer.Write((byte)7);
            writer.Write((byte)speakerSlot);
            WriteMapleString(writer, text);
            return stream.ToArray();
        }

        private static byte[] BuildMiniRoomLeavePacket(int slot)
        {
            return new byte[] { 10, (byte)slot };
        }

        private static void WriteMapleString(BinaryWriter writer, string text)
        {
            string value = text ?? string.Empty;
            writer.Write((short)value.Length);
            writer.Write(System.Text.Encoding.ASCII.GetBytes(value));
        }
    }
}
