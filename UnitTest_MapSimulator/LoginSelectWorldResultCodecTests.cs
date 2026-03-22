using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Managers;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace UnitTest_MapSimulator
{
    public class LoginSelectWorldResultCodecTests
    {
        [Fact]
        public void TryDecode_SuccessPayloadDecodesRosterEntry()
        {
            LoginAvatarLook look = LoginAvatarLookCodec.CreateLook(
                CharacterGender.Female,
                SkinColor.Light,
                21000,
                31000,
                new Dictionary<EquipSlot, int>
                {
                    [EquipSlot.Cap] = 1002140,
                    [EquipSlot.Coat] = 1042003,
                    [EquipSlot.Weapon] = 1322013
                });

            byte[] payload = BuildSelectWorldResultPacket(
                resultCode: 0,
                jobId: 111,
                avatarLook: look,
                characterId: 123456,
                name: "PacketMage",
                level: 37,
                fieldMapId: 100000000,
                portal: 2,
                exp: 98765,
                fame: 12,
                worldRank: 44,
                jobRank: 9);

            bool decoded = LoginSelectWorldResultCodec.TryDecode(payload, out LoginSelectWorldResultProfile profile, out string error);

            Assert.True(decoded, error);
            Assert.NotNull(profile);
            Assert.Equal(0, profile.ResultCode);
            Assert.True(profile.LoginOpt);
            Assert.Equal(8, profile.SlotCount);
            Assert.Equal(1, profile.BuyCharacterCount);
            Assert.Single(profile.Entries);

            LoginSelectWorldCharacterEntry entry = profile.Entries[0];
            Assert.Equal(123456, entry.CharacterId);
            Assert.Equal("PacketMage", entry.Name);
            Assert.Equal(CharacterGender.Female, entry.Gender);
            Assert.Equal(37, entry.Level);
            Assert.Equal(111, entry.JobId);
            Assert.Equal(100000000, entry.FieldMapId);
            Assert.Equal((byte)2, entry.Portal);
            Assert.Equal(44, entry.WorldRank);
            Assert.Equal(9, entry.JobRank);
            Assert.Equal(21000, entry.AvatarLook.FaceId);
            Assert.Equal(31000, entry.AvatarLook.HairId);
            Assert.Equal(1002140, entry.AvatarLook.VisibleEquipmentByBodyPart[1]);
        }

        [Fact]
        public void TryDecode_ExtendedSpPayloadSkipsSpSetBytes()
        {
            LoginAvatarLook look = LoginAvatarLookCodec.CreateLook(
                CharacterGender.Male,
                SkinColor.Light,
                20000,
                30000,
                new Dictionary<EquipSlot, int>
                {
                    [EquipSlot.Weapon] = 1322013
                });

            byte[] payload = BuildSelectWorldResultPacket(
                resultCode: 12,
                jobId: 3500,
                avatarLook: look,
                characterId: 7,
                name: "Mechanic",
                level: 20,
                fieldMapId: 910000000,
                portal: 0,
                exp: 1111,
                fame: 3,
                worldRank: 0,
                jobRank: 0);

            bool decoded = LoginSelectWorldResultCodec.TryDecode(payload, out LoginSelectWorldResultProfile profile, out string error);

            Assert.True(decoded, error);
            Assert.NotNull(profile);
            Assert.Equal(12, profile.ResultCode);
            Assert.Single(profile.Entries);
            Assert.Equal(3500, profile.Entries[0].JobId);
            Assert.Equal(1322013, profile.Entries[0].AvatarLook.VisibleEquipmentByBodyPart[11]);
        }

        [Fact]
        public void TryDecode_FailurePacketReturnsResultCodeWithoutRoster()
        {
            bool decoded = LoginSelectWorldResultCodec.TryDecode(new byte[] { 7 }, out LoginSelectWorldResultProfile profile, out string error);

            Assert.True(decoded, error);
            Assert.NotNull(profile);
            Assert.Equal(7, profile.ResultCode);
            Assert.Empty(profile.Entries);
        }

        [Fact]
        public void TryDecode_AvatarLookPreservesWeaponSticker()
        {
            LoginAvatarLook look = LoginAvatarLookCodec.CreateLook(
                CharacterGender.Female,
                SkinColor.Light,
                21000,
                31000,
                new Dictionary<EquipSlot, int>
                {
                    [EquipSlot.Weapon] = 1322013
                },
                weaponStickerItemId: 1702174);

            byte[] payload = BuildSelectWorldResultPacket(
                resultCode: 0,
                jobId: 111,
                avatarLook: look,
                characterId: 321,
                name: "Sticker",
                level: 12,
                fieldMapId: 100000000,
                portal: 0,
                exp: 99,
                fame: 1,
                worldRank: 0,
                jobRank: 0);

            bool decoded = LoginSelectWorldResultCodec.TryDecode(payload, out LoginSelectWorldResultProfile profile, out string error);

            Assert.True(decoded, error);
            Assert.Equal(1702174, profile.Entries[0].AvatarLook.WeaponStickerItemId);
        }

        private static byte[] BuildSelectWorldResultPacket(
            byte resultCode,
            int jobId,
            LoginAvatarLook avatarLook,
            int characterId,
            string name,
            byte level,
            int fieldMapId,
            byte portal,
            int exp,
            short fame,
            int worldRank,
            int jobRank)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            writer.Write(resultCode);
            writer.Write((byte)1);
            WriteCharacterStat(writer, characterId, name, avatarLook, level, (short)jobId, exp, fame, fieldMapId, portal);
            writer.Write(LoginAvatarLookCodec.Encode(avatarLook));
            writer.Write((byte)1);
            writer.Write((byte)1);
            writer.Write(worldRank);
            writer.Write(0);
            writer.Write(jobRank);
            writer.Write(0);
            writer.Write((byte)1);
            writer.Write(8);
            writer.Write(1);
            writer.Flush();
            return stream.ToArray();
        }

        private static void WriteCharacterStat(
            BinaryWriter writer,
            int characterId,
            string name,
            LoginAvatarLook avatarLook,
            byte level,
            short jobId,
            int exp,
            short fame,
            int fieldMapId,
            byte portal)
        {
            writer.Write(characterId);
            byte[] nameBytes = new byte[13];
            byte[] sourceName = System.Text.Encoding.ASCII.GetBytes(name);
            System.Array.Copy(sourceName, nameBytes, System.Math.Min(sourceName.Length, nameBytes.Length));
            writer.Write(nameBytes);
            writer.Write((byte)avatarLook.Gender);
            writer.Write((byte)avatarLook.Skin);
            writer.Write(avatarLook.FaceId);
            writer.Write(avatarLook.HairId);
            writer.Write(new byte[24]);
            writer.Write(level);
            writer.Write(jobId);
            writer.Write((short)4);
            writer.Write((short)5);
            writer.Write((short)6);
            writer.Write((short)7);
            writer.Write(123);
            writer.Write(456);
            writer.Write(78);
            writer.Write(90);
            writer.Write((short)3);

            if (jobId / 1000 == 3 || jobId / 100 == 22 || jobId == 2001)
            {
                writer.Write((byte)2);
                writer.Write((byte)1);
                writer.Write((byte)10);
                writer.Write((byte)2);
                writer.Write((byte)20);
            }
            else
            {
                writer.Write((short)8);
            }

            writer.Write(exp);
            writer.Write(fame);
            writer.Write(0);
            writer.Write(fieldMapId);
            writer.Write(portal);
            writer.Write(3600);
            writer.Write((short)0);
        }
    }
}
