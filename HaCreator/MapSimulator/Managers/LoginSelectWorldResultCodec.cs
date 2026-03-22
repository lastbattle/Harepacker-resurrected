using HaCreator.MapSimulator.Character;
using MapleLib.PacketLib;
using System;
using System.Collections.Generic;
using System.IO;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class LoginSelectWorldCharacterEntry
    {
        public int CharacterId { get; init; }
        public int? WorldId { get; init; }
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
        public int? WorldRankMove { get; init; }
        public int? JobRank { get; init; }
        public int? JobRankMove { get; init; }
        public LoginAvatarLook AvatarLook { get; init; }
        public byte[] AvatarLookPacket { get; init; } = Array.Empty<byte>();

        public int? PreviousWorldRank =>
            WorldRank.HasValue && WorldRank.Value > 0 && WorldRankMove.HasValue
                ? Math.Max(1, WorldRank.Value + WorldRankMove.Value)
                : null;

        public int? PreviousJobRank =>
            JobRank.HasValue && JobRank.Value > 0 && JobRankMove.HasValue
                ? Math.Max(1, JobRank.Value + JobRankMove.Value)
                : null;
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
            return LoginCharacterStatCodec.DecodeCharacterEntry(reader);
        }

        public static bool IsSuccessCode(byte resultCode)
        {
            return resultCode == 0 || resultCode == 12 || resultCode == 23;
        }
    }
}
