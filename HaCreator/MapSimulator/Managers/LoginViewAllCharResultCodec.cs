using MapleLib.PacketLib;
using System;
using System.Collections.Generic;
using System.IO;

namespace HaCreator.MapSimulator.Managers
{
    public enum LoginViewAllCharResultKind
    {
        Characters,
        Header,
        Completion,
        Error
    }

    public sealed class LoginViewAllCharResultPacketProfile
    {
        public byte ResultCode { get; init; }
        public LoginViewAllCharResultKind Kind { get; init; }
        public int RelatedServerCount { get; init; }
        public int CharacterCount { get; init; }
        public int? WorldId { get; init; }
        public IReadOnlyList<LoginSelectWorldCharacterEntry> Entries { get; init; } = Array.Empty<LoginSelectWorldCharacterEntry>();
        public bool? LoginOpt { get; init; }
    }

    public static class LoginViewAllCharResultCodec
    {
        public static bool TryDecode(byte[] data, out LoginViewAllCharResultPacketProfile profile, out string error)
        {
            profile = null;
            error = string.Empty;

            if (data == null || data.Length == 0)
            {
                error = "ViewAllCharResult payload is empty.";
                return false;
            }

            try
            {
                return TryDecode(new PacketReader(data), out profile, out error);
            }
            catch (EndOfStreamException)
            {
                error = "ViewAllCharResult payload ended before decoding completed.";
                return false;
            }
            catch (IOException)
            {
                error = "ViewAllCharResult payload could not be read.";
                return false;
            }
        }

        public static bool TryDecode(PacketReader reader, out LoginViewAllCharResultPacketProfile profile, out string error)
        {
            profile = null;
            error = string.Empty;
            if (reader == null)
            {
                error = "ViewAllCharResult reader is missing.";
                return false;
            }

            try
            {
                byte resultCode = reader.ReadByte();
                switch (resultCode)
                {
                    case 0:
                    {
                        int worldId = reader.ReadByte();
                        int characterCount = reader.ReadByte();
                        List<LoginSelectWorldCharacterEntry> entries = new(characterCount);
                        for (int index = 0; index < characterCount; index++)
                        {
                            LoginSelectWorldCharacterEntry entry = LoginCharacterStatCodec.DecodeCharacterEntry(reader);
                            entries.Add(new LoginSelectWorldCharacterEntry
                            {
                                CharacterId = entry.CharacterId,
                                WorldId = worldId,
                                Name = entry.Name,
                                Gender = entry.Gender,
                                Skin = entry.Skin,
                                FaceId = entry.FaceId,
                                HairId = entry.HairId,
                                Level = entry.Level,
                                JobId = entry.JobId,
                                SubJob = entry.SubJob,
                                Strength = entry.Strength,
                                Dexterity = entry.Dexterity,
                                Intelligence = entry.Intelligence,
                                Luck = entry.Luck,
                                AbilityPoints = entry.AbilityPoints,
                                HitPoints = entry.HitPoints,
                                MaxHitPoints = entry.MaxHitPoints,
                                ManaPoints = entry.ManaPoints,
                                MaxManaPoints = entry.MaxManaPoints,
                                Experience = entry.Experience,
                                Fame = entry.Fame,
                                FieldMapId = entry.FieldMapId,
                                Portal = entry.Portal,
                                PlayTime = entry.PlayTime,
                                OnFamily = entry.OnFamily,
                                WorldRank = entry.WorldRank,
                                JobRank = entry.JobRank,
                                AvatarLook = entry.AvatarLook,
                                AvatarLookPacket = entry.AvatarLookPacket
                            });
                        }

                        profile = new LoginViewAllCharResultPacketProfile
                        {
                            ResultCode = resultCode,
                            Kind = LoginViewAllCharResultKind.Characters,
                            WorldId = worldId,
                            CharacterCount = characterCount,
                            Entries = entries,
                            LoginOpt = TryReadOptionalLoginOpt(reader)
                        };
                        return true;
                    }

                    case 1:
                        profile = new LoginViewAllCharResultPacketProfile
                        {
                            ResultCode = resultCode,
                            Kind = LoginViewAllCharResultKind.Header,
                            RelatedServerCount = reader.ReadInt(),
                            CharacterCount = reader.ReadInt()
                        };
                        return true;

                    case 4:
                    case 5:
                        profile = new LoginViewAllCharResultPacketProfile
                        {
                            ResultCode = resultCode,
                            Kind = LoginViewAllCharResultKind.Completion
                        };
                        return true;

                    default:
                        profile = new LoginViewAllCharResultPacketProfile
                        {
                            ResultCode = resultCode,
                            Kind = LoginViewAllCharResultKind.Error
                        };
                        return true;
                }
            }
            catch (EndOfStreamException)
            {
                error = "ViewAllCharResult payload ended before decoding completed.";
                return false;
            }
            catch (IOException)
            {
                error = "ViewAllCharResult payload could not be read.";
                return false;
            }
        }

        private static bool? TryReadOptionalLoginOpt(PacketReader reader)
        {
            try
            {
                return reader.ReadByte() != 0;
            }
            catch (EndOfStreamException)
            {
                return null;
            }
        }
    }
}
