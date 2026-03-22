using System.Collections.Generic;
using System.IO;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
using Xunit;

namespace UnitTest_MapSimulator
{
    public class MapleTvRuntimeTests
    {
        [Fact]
        public void SetItem_KnownCashVariantsApplyAudienceAndDurationProfile()
        {
            MapleTvRuntime runtime = CreateRuntime();

            runtime.SetItem(5075001, "MapleTV Star Messenger");
            MapleTvSnapshot starSnapshot = runtime.BuildSnapshot(0);
            Assert.False(starSnapshot.UseReceiver);
            Assert.Equal(30000, starSnapshot.TotalWaitMs);
            Assert.NotEqual(starSnapshot.DefaultMediaIndex, starSnapshot.ResolvedMediaIndex);

            runtime.SetItem(5075002, "MapleTV Heart Messenger");
            MapleTvSnapshot heartSnapshot = runtime.BuildSnapshot(0);
            Assert.True(heartSnapshot.UseReceiver);
            Assert.Equal(60000, heartSnapshot.TotalWaitMs);
            Assert.NotEqual(starSnapshot.ResolvedMediaIndex, heartSnapshot.ResolvedMediaIndex);
        }

        [Fact]
        public void OnSetMessage_ReceiverRequiredItemRejectsSendWithoutReceiver()
        {
            MapleTvRuntime runtime = CreateRuntime();
            runtime.SetItem(5075002, "MapleTV Heart Messenger");
            runtime.SetDraftLine(1, "Proposal pending.");

            string message = runtime.OnSetMessage(1000);

            Assert.Equal("This MapleTV item requires a receiver before sending.", message);
        }

        [Fact]
        public void TryApplySetMessagePacket_DecodesReceiverAvatarLookAndPayloadLines()
        {
            MapleTvRuntime runtime = CreateRuntime();
            byte[] payload = BuildSetMessagePacket(
                flag: 2,
                messageType: 2,
                senderLook: CreateLook(CharacterGender.Male, faceId: 20000, hairId: 30000),
                senderName: "Broadcaster",
                receiverName: "Viewer",
                lines: new[] { "Line 1", "Line 2", "Line 3", "Line 4", "Line 5" },
                totalWaitTime: 15000,
                receiverLook: CreateLook(CharacterGender.Female, faceId: 21000, hairId: 31000));

            bool decoded = runtime.TryApplySetMessagePacket(payload, 1234, CreateBuildFromLook, out string message);

            Assert.True(decoded, message);
            MapleTvSnapshot snapshot = runtime.BuildSnapshot(1234);
            Assert.True(snapshot.IsShowingMessage);
            Assert.True(snapshot.QueueExists);
            Assert.True(snapshot.UseReceiver);
            Assert.False(snapshot.IsSelfMessage);
            Assert.Equal(2, snapshot.MessageType);
            Assert.Equal("Broadcaster", snapshot.SenderName);
            Assert.Equal("Viewer", snapshot.ReceiverName);
            Assert.Equal("Line 1", snapshot.DisplayLines[0]);
            Assert.Equal(15000, snapshot.RemainingMs);
            Assert.Equal(20000, snapshot.SenderBuild.Face.ItemId);
            Assert.Equal(31000, snapshot.ReceiverBuild.Hair.ItemId);
        }

        [Fact]
        public void TryApplySendMessageResultPacket_MapsRecipientOfflineCode()
        {
            MapleTvRuntime runtime = CreateRuntime();

            bool decoded = runtime.TryApplySendMessageResultPacket(new byte[] { 1, 2 }, out string message);

            Assert.True(decoded, message);
            Assert.Equal("MapleTV send request rejected because the target recipient is unavailable.", message);
        }

        private static MapleTvRuntime CreateRuntime()
        {
            MapleTvRuntime runtime = new();
            runtime.ConfigureDefaultMedia(0, "Maple TV", defaultMediaIndex: 1);
            runtime.UpdateLocalContext(new CharacterBuild
            {
                Name = "Sender",
                Gender = CharacterGender.Male,
                Skin = SkinColor.Light,
                Face = new FacePart { ItemId = 20000 },
                Hair = new HairPart { ItemId = 30000 },
                Equipment = new Dictionary<EquipSlot, CharacterPart>()
            });
            return runtime;
        }

        private static LoginAvatarLook CreateLook(CharacterGender gender, int faceId, int hairId)
        {
            return LoginAvatarLookCodec.CreateLook(
                gender,
                SkinColor.Light,
                faceId,
                hairId,
                new Dictionary<EquipSlot, int>());
        }

        private static CharacterBuild CreateBuildFromLook(LoginAvatarLook look)
        {
            return new CharacterBuild
            {
                Gender = look.Gender,
                Skin = look.Skin,
                Face = new FacePart { ItemId = look.FaceId },
                Hair = new HairPart { ItemId = look.HairId },
                Equipment = new Dictionary<EquipSlot, CharacterPart>()
            };
        }

        private static byte[] BuildSetMessagePacket(
            byte flag,
            byte messageType,
            LoginAvatarLook senderLook,
            string senderName,
            string receiverName,
            IReadOnlyList<string> lines,
            int totalWaitTime,
            LoginAvatarLook receiverLook)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream);
            writer.Write(flag);
            writer.Write(messageType);
            writer.Write(LoginAvatarLookCodec.Encode(senderLook));
            WriteMapleString(writer, senderName);
            WriteMapleString(writer, receiverName);
            for (int i = 0; i < 5; i++)
            {
                WriteMapleString(writer, i < lines.Count ? lines[i] : string.Empty);
            }

            writer.Write(totalWaitTime);
            if ((flag & 2) != 0 && receiverLook != null)
            {
                writer.Write(LoginAvatarLookCodec.Encode(receiverLook));
            }

            writer.Flush();
            return stream.ToArray();
        }

        private static void WriteMapleString(BinaryWriter writer, string text)
        {
            string value = text ?? string.Empty;
            writer.Write((short)value.Length);
            writer.Write(System.Text.Encoding.ASCII.GetBytes(value));
        }
    }
}
