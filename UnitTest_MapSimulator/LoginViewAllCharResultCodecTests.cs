using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Managers;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace UnitTest_MapSimulator
{
    public class LoginViewAllCharResultCodecTests
    {
        [Fact]
        public void TryDecode_HeaderPacketDecodesCounts()
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write((byte)1);
            writer.Write(2);
            writer.Write(5);
            writer.Flush();

            bool decoded = LoginViewAllCharResultCodec.TryDecode(stream.ToArray(), out LoginViewAllCharResultPacketProfile profile, out string error);

            Assert.True(decoded, error);
            Assert.Equal(LoginViewAllCharResultKind.Header, profile.Kind);
            Assert.Equal(2, profile.RelatedServerCount);
            Assert.Equal(5, profile.CharacterCount);
        }

        [Fact]
        public void TryDecode_CharacterChunkDecodesWorldOwnedEntries()
        {
            LoginAvatarLook look = LoginAvatarLookCodec.CreateLook(
                CharacterGender.Male,
                SkinColor.Light,
                20000,
                30000,
                new Dictionary<EquipSlot, int>
                {
                    [EquipSlot.Weapon] = 1322013
                },
                weaponStickerItemId: 1702174);

            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write((byte)0);
            writer.Write((byte)7);
            writer.Write((byte)1);
            WriteCharacterStat(writer, 44, "VACOne", look, 23, 211, 100000000);
            writer.Write(LoginAvatarLookCodec.Encode(look));
            writer.Write((byte)1);
            writer.Write(12);
            writer.Write(0);
            writer.Write(3);
            writer.Write(0);
            writer.Write((byte)1);
            writer.Flush();

            bool decoded = LoginViewAllCharResultCodec.TryDecode(stream.ToArray(), out LoginViewAllCharResultPacketProfile profile, out string error);

            Assert.True(decoded, error);
            Assert.Equal(LoginViewAllCharResultKind.Characters, profile.Kind);
            Assert.Equal(7, profile.WorldId);
            Assert.True(profile.LoginOpt);
            Assert.Single(profile.Entries);
            Assert.Equal(7, profile.Entries[0].WorldId);
            Assert.Equal(1702174, profile.Entries[0].AvatarLook.WeaponStickerItemId);
        }

        private static void WriteCharacterStat(
            BinaryWriter writer,
            int characterId,
            string name,
            LoginAvatarLook avatarLook,
            byte level,
            short jobId,
            int fieldMapId)
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
            writer.Write((short)8);
            writer.Write(999);
            writer.Write((short)4);
            writer.Write(0);
            writer.Write(fieldMapId);
            writer.Write((byte)1);
            writer.Write(1800);
            writer.Write((short)0);
        }
    }
}
