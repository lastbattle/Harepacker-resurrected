using HaCreator.MapSimulator.Character;
using MapleLib.PacketLib;
using System;
using System.IO;
using System.Text;

namespace HaCreator.MapSimulator.Managers
{
    internal static class LoginCharacterStatCodec
    {
        private const int CharacterNameLength = 13;
        private const int PetLockerSerialNumberLength = 24;
        private const int RankBlockLength = 16;

        public static LoginSelectWorldCharacterEntry DecodeCharacterEntry(PacketReader reader)
        {
            return DecodeCharacterEntry(reader, includeFamilyAndRank: true);
        }

        public static LoginSelectWorldCharacterEntry DecodeCharacterEntryForCreateNewCharacter(PacketReader reader)
        {
            return DecodeCharacterEntry(reader, includeFamilyAndRank: false);
        }

        private static LoginSelectWorldCharacterEntry DecodeCharacterEntry(PacketReader reader, bool includeFamilyAndRank)
        {
            int characterId = reader.ReadInt();
            string name = ReadFixedAsciiString(reader, CharacterNameLength);
            CharacterGender gender = DecodeGender(reader.ReadByte());
            SkinColor skin = DecodeSkin(reader.ReadByte());
            int faceId = reader.ReadInt();
            int hairId = reader.ReadInt();
            reader.Skip(PetLockerSerialNumberLength);

            int level = reader.ReadByte();
            int jobId = reader.ReadShort();
            int strength = reader.ReadShort();
            int dexterity = reader.ReadShort();
            int intelligence = reader.ReadShort();
            int luck = reader.ReadShort();
            int hp = reader.ReadInt();
            int maxHp = reader.ReadInt();
            int mp = reader.ReadInt();
            int maxMp = reader.ReadInt();
            int abilityPoints = reader.ReadShort();

            SkipSkillPoints(reader, jobId);

            long experience = reader.ReadInt();
            int fame = reader.ReadShort();
            reader.ReadInt();
            int fieldMapId = reader.ReadInt();
            byte portal = reader.ReadByte();
            int playTime = reader.ReadInt();
            int subJob = reader.ReadShort();

            if (!LoginAvatarLookCodec.TryDecode(reader, out LoginAvatarLook avatarLook, out string avatarError))
            {
                throw new InvalidDataException(avatarError);
            }

            bool onFamily = false;
            LoginCharacterRankInfo rankInfo = LoginCharacterRankInfo.Empty;
            if (includeFamilyAndRank)
            {
                onFamily = reader.ReadByte() != 0;
                rankInfo = ReadRankBlock(reader);
            }

            return new LoginSelectWorldCharacterEntry
            {
                CharacterId = characterId,
                Name = name,
                Gender = gender,
                Skin = skin,
                FaceId = faceId,
                HairId = hairId,
                Level = level,
                JobId = jobId,
                SubJob = subJob,
                Strength = strength,
                Dexterity = dexterity,
                Intelligence = intelligence,
                Luck = luck,
                AbilityPoints = abilityPoints,
                HitPoints = hp,
                MaxHitPoints = maxHp,
                ManaPoints = mp,
                MaxManaPoints = maxMp,
                Experience = experience,
                Fame = fame,
                FieldMapId = fieldMapId,
                Portal = portal,
                PlayTime = playTime,
                OnFamily = onFamily,
                WorldRank = rankInfo.WorldRank,
                WorldRankMove = rankInfo.WorldRankMove,
                JobRank = rankInfo.JobRank,
                JobRankMove = rankInfo.JobRankMove,
                AvatarLook = avatarLook,
                AvatarLookPacket = LoginAvatarLookCodec.Encode(avatarLook)
            };
        }

        private static void SkipSkillPoints(PacketReader reader, int jobId)
        {
            if (UsesExtendedSkillPoints(jobId))
            {
                int count = reader.ReadByte();
                reader.Skip(count * 2);
                return;
            }

            reader.ReadShort();
        }

        private static bool UsesExtendedSkillPoints(int jobId)
        {
            return jobId / 1000 == 3 || jobId / 100 == 22 || jobId == 2001;
        }

        private static LoginCharacterRankInfo ReadRankBlock(PacketReader reader)
        {
            if (reader.ReadByte() == 0)
            {
                return LoginCharacterRankInfo.Empty;
            }

            byte[] rankBytes = reader.ReadBytes(RankBlockLength);
            if (rankBytes.Length < RankBlockLength)
            {
                throw new EndOfStreamException();
            }

            int worldRank = BitConverter.ToInt32(rankBytes, 0);
            int worldRankMove = BitConverter.ToInt32(rankBytes, 4);
            int jobRank = BitConverter.ToInt32(rankBytes, 8);
            int jobRankMove = BitConverter.ToInt32(rankBytes, 12);
            return new LoginCharacterRankInfo(
                worldRank > 0 ? worldRank : null,
                worldRank > 0 ? worldRankMove : null,
                jobRank > 0 ? jobRank : null,
                jobRank > 0 ? jobRankMove : null);
        }

        private readonly record struct LoginCharacterRankInfo(
            int? WorldRank,
            int? WorldRankMove,
            int? JobRank,
            int? JobRankMove)
        {
            public static LoginCharacterRankInfo Empty => new(null, null, null, null);
        }

        private static string ReadFixedAsciiString(PacketReader reader, int length)
        {
            byte[] bytes = reader.ReadBytes(length);
            int terminatorIndex = Array.IndexOf(bytes, (byte)0);
            int effectiveLength = terminatorIndex >= 0 ? terminatorIndex : bytes.Length;
            return Encoding.ASCII.GetString(bytes, 0, effectiveLength).Trim();
        }

        private static CharacterGender DecodeGender(byte rawGender)
        {
            return Enum.IsDefined(typeof(CharacterGender), (int)rawGender)
                ? (CharacterGender)rawGender
                : CharacterGender.Male;
        }

        private static SkinColor DecodeSkin(byte rawSkin)
        {
            return Enum.IsDefined(typeof(SkinColor), (int)rawSkin)
                ? (SkinColor)rawSkin
                : SkinColor.Light;
        }
    }
}
