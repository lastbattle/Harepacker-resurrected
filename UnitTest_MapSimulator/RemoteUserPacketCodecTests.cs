using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Physics;
using HaCreator.MapSimulator.Pools;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace UnitTest_MapSimulator
{
    public sealed class RemoteUserPacketCodecTests
    {
        [Fact]
        public void TryParseEnterField_DecodesAvatarLookAndPlacement()
        {
            LoginAvatarLook look = LoginAvatarLookCodec.CreateLook(
                CharacterGender.Female,
                SkinColor.Light,
                21000,
                31000,
                Enumerable.Empty<KeyValuePair<EquipSlot, int>>());
            byte[] avatarPayload = LoginAvatarLookCodec.Encode(look);

            using MemoryStream stream = new();
            using (BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(321);
                writer.Write((short)120);
                writer.Write((short)240);
                writer.Write((byte)0);
                writer.Write((byte)1);
                WriteString8(writer, "Ayla");
                WriteString8(writer, "stand1");
                writer.Write(avatarPayload.Length);
                writer.Write(avatarPayload);
            }

            bool parsed = RemoteUserPacketCodec.TryParseEnterField(stream.ToArray(), out RemoteUserEnterFieldPacket packet, out string error);

            Assert.True(parsed, error);
            Assert.Equal(321, packet.CharacterId);
            Assert.Equal("Ayla", packet.Name);
            Assert.Equal((short)120, packet.X);
            Assert.Equal((short)240, packet.Y);
            Assert.True(packet.FacingRight);
            Assert.True(packet.IsVisibleInWorld);
            Assert.Equal("stand1", packet.ActionName);
            Assert.Equal(CharacterGender.Female, packet.AvatarLook.Gender);
            Assert.Equal(21000, packet.AvatarLook.FaceId);
            Assert.Equal(31000, packet.AvatarLook.HairId);
        }

        [Fact]
        public void TryParseMove_DecodesMovementSnapshotAndMoveAction()
        {
            using MemoryStream stream = new();
            using (BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(7);
                writer.Write((short)10);
                writer.Write((short)20);
                writer.Write((short)1);
                writer.Write((short)2);
                writer.Write((byte)1);
                writer.Write((byte)0);
                writer.Write((short)40);
                writer.Write((short)60);
                writer.Write((short)3);
                writer.Write((short)4);
                writer.Write((short)9);
                writer.Write((short)0);
                writer.Write((short)0);
                writer.Write((byte)((byte)MoveAction.Walk << 1));
                writer.Write((short)30);
            }

            bool parsed = RemoteUserPacketCodec.TryParseMove(stream.ToArray(), 1000, out RemoteUserMovePacket packet, out string error);

            Assert.True(parsed, error);
            Assert.Equal(7, packet.CharacterId);
            Assert.Equal((byte)((byte)MoveAction.Walk << 1), packet.MoveAction);
            Assert.Single(packet.Snapshot.MovePath);
            Assert.Equal(40, packet.Snapshot.MovePath[0].X);
            Assert.Equal(60, packet.Snapshot.MovePath[0].Y);
            Assert.Equal(MoveAction.Walk, packet.Snapshot.MovePath[0].Action);
            Assert.True(packet.Snapshot.MovePath[0].FacingRight);
            Assert.Equal(1030, packet.Snapshot.PassivePosition.TimeStamp);
            Assert.Equal(MoveAction.Walk, packet.Snapshot.PassivePosition.Action);
        }

        [Fact]
        public void TryApplyMoveSnapshot_UpdatesRemoteActorPositionAndAction()
        {
            RemoteUserActorPool pool = new();
            CharacterBuild build = new() { Name = "Remote1" };

            bool added = pool.TryAddOrUpdate(1, build, Vector2.Zero, out string addMessage, true, null, "test", true);

            Assert.True(added, addMessage);

            PlayerMovementSyncSnapshot snapshot = new(
                new PassivePositionSnapshot
                {
                    X = 60,
                    Y = 80,
                    Action = MoveAction.Walk,
                    TimeStamp = 1030,
                    FacingRight = false
                },
                new List<MovePathElement>
                {
                    new()
                    {
                        X = 20,
                        Y = 30,
                        Action = MoveAction.Walk,
                        TimeStamp = 1000,
                        Duration = 30,
                        FacingRight = true
                    }
                });

            bool applied = pool.TryApplyMoveSnapshot(1, snapshot, (byte)(((byte)MoveAction.Walk << 1) | 1), 1000, out string applyMessage);

            Assert.True(applied, applyMessage);
            Assert.True(pool.TryGetActor(1, out RemoteUserActor actor));
            Assert.Equal(new Vector2(20f, 30f), actor.Position);
            Assert.Equal("walk1", actor.ActionName);
            Assert.True(actor.FacingRight);

            pool.Update(1040);

            Assert.Equal(new Vector2(60f, 80f), actor.Position);
            Assert.Equal("walk1", actor.ActionName);
            Assert.False(actor.FacingRight);
        }

        private static void WriteString8(BinaryWriter writer, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            writer.Write((byte)bytes.Length);
            writer.Write(bytes);
        }
    }
}
