using HaCreator.MapSimulator.Character;
using MapleLib.PacketLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class LoginSelectWorldCharacterEntry
    {
        public int CharacterId { get; init; }
        public string Name { get; init; } = string.Empty;
        public CharacterGender Gender { get; init; }
        public SkinColor Skin { get; init; }
        public int FaceId { get; init; }
        public int HairId { get; init; }
        public int Level { get; init; }
        public int JobId { get; init; }
        public int SubJob { get; init; }
        public int Strength { get; init; }
        public int Dexterity { get; init; }
        public int Intelligence { get; init; }
        public int Luck { get; init; }
        public int AbilityPoints { get; init; }
        public int HitPoints { get; init; }
        public int MaxHitPoints { get; init; }
        public int ManaPoints { get; init; }
        public int MaxManaPoints { get; init; }
        public long Experience { get; init; }
        public int Fame { get; init; }
        public int FieldMapId { get; init; }
        public byte Portal { get; init; }
        public int PlayTime { get; init; }
        public bool OnFamily { get; init; }
        public int? WorldRank { get; init; }
        public int? JobRank { get; init; }
        public LoginAvatarLook AvatarLook { get; init; }
        public byte[] AvatarLookPacket { get; init; } = Array.Empty<byte>();
    }

    public sealed class LoginSelectWorldResultProfile
    {
        public byte ResultCode { get; init; }
        public IReadOnlyList<LoginSelectWorldCharacterEntry> Entries { get; init; } = Array.Empty<LoginSelectWorldCharacterEntry>();
        public bool LoginOpt { get; init; }
        public int SlotCount { get; init; }
        public int BuyCharacterCount { get; init; }
    }

    public static class LoginSelectWorldResultCodec
    {
        private const int CharacterNameLength = 13;
        private const int PetLockerSerialNumberLength = 24;
        private const int RankBlockLength = 16;
        private const int MaxCharacterCount = 15;

        public static bool TryDecode(byte[] data, out LoginSelectWorldResultProfile profile, out string error)
        {
            profile = null;
            error = string.Empty;

            if (data == null || data.Length == 0)
            {
                error = "SelectWorldResult payload is empty.";
                return false;
            }

            try
            {
                return TryDecode(new PacketReader(data), out profile, out error);
            }
            catch (EndOfStreamException)
            {
                error = "SelectWorldResult payload ended before decoding completed.";
                return false;
            }
            catch (IOException)
            {
                error = "SelectWorldResult payload could not be read.";
                return false;
            }
        }

        public static bool TryDecode(PacketReader reader, out LoginSelectWorldResultProfile profile, out string error)
        {
            profile = null;
            error = string.Empty;
            if (reader == null)
            {
                error = "SelectWorldResult reader is missing.";
                return false;
            }

            try
            {
                byte resultCode = reader.ReadByte();
                if (!IsSuccessCode(resultCode))
                {
                    profile = new LoginSelectWorldResultProfile
                    {
                        ResultCode = resultCode
                    };

                    return true;
                }

                int characterCount = reader.ReadByte();
                var entries = new List<LoginSelectWorldCharacterEntry>(Math.Min(characterCount, MaxCharacterCount));
                for (int index = 0; index < characterCount; index++)
                {
                    LoginSelectWorldCharacterEntry entry = DecodeCharacterEntry(reader);
                    if (index < MaxCharacterCount)
                    {
                        entries.Add(entry);
                    }
                }

                profile = new LoginSelectWorldResultProfile
                {
                    ResultCode = resultCode,
                    Entries = entries,
                    LoginOpt = reader.ReadByte() != 0,
                    SlotCount = reader.ReadInt(),
                    BuyCharacterCount = reader.ReadInt()
                };
                return true;
            }
            catch (EndOfStreamException)
            {
                error = "SelectWorldResult payload ended before decoding completed.";
                return false;
            }
            catch (IOException)
            {
                error = "SelectWorldResult payload could not be read.";
                return false;
            }
        }

        private static LoginSelectWorldCharacterEntry DecodeCharacterEntry(PacketReader reader)
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

            bool onFamily = reader.ReadByte() != 0;
            (int? worldRank, int? jobRank) = ReadRankBlock(reader);

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
                WorldRank = worldRank,
                JobRank = jobRank,
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

        private static (int? worldRank, int? jobRank) ReadRankBlock(PacketReader reader)
        {
            if (reader.ReadByte() == 0)
            {
                return (null, null);
            }

            byte[] rankBytes = reader.ReadBytes(RankBlockLength);
            if (rankBytes.Length < RankBlockLength)
            {
                throw new EndOfStreamException();
            }

            int worldRank = BitConverter.ToInt32(rankBytes, 0);
            int jobRank = BitConverter.ToInt32(rankBytes, 8);
            return (worldRank > 0 ? worldRank : null, jobRank > 0 ? jobRank : null);
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

        public static bool IsSuccessCode(byte resultCode)
        {
            return resultCode == 0 || resultCode == 12 || resultCode == 23;
        }
    }
}
