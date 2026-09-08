using MapleLib.PacketLib;
using System;
using System.IO;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class LoginCreateNewCharacterResultProfile
    {
        public byte ResultCode { get; init; }
        public byte[] Payload { get; init; } = Array.Empty<byte>();
        public LoginSelectWorldCharacterEntry CreatedCharacter { get; init; }

        public bool IsSuccess => ResultCode == 0;
    }

    public static class LoginCreateNewCharacterResultCodec
    {
        public static bool TryDecode(byte[] data, out LoginCreateNewCharacterResultProfile profile, out string error)
        {
            profile = null;
            error = string.Empty;

            if (data == null || data.Length == 0)
            {
                error = "CreateNewCharacterResult payload is empty.";
                return false;
            }

            try
            {
                bool decoded = TryDecode(new PacketReader(data), out profile, out error);
                if (decoded && profile != null)
                {
                    profile = new LoginCreateNewCharacterResultProfile
                    {
                        ResultCode = profile.ResultCode,
                        Payload = (byte[])data.Clone(),
                        CreatedCharacter = profile.CreatedCharacter
                    };
                }

                return decoded;
            }
            catch (EndOfStreamException)
            {
                error = "CreateNewCharacterResult payload ended before decoding completed.";
                return false;
            }
            catch (IOException)
            {
                error = "CreateNewCharacterResult payload could not be read.";
                return false;
            }
        }

        public static bool TryDecode(PacketReader reader, out LoginCreateNewCharacterResultProfile profile, out string error)
        {
            profile = null;
            error = string.Empty;
            if (reader == null)
            {
                error = "CreateNewCharacterResult reader is missing.";
                return false;
            }

            try
            {
                byte resultCode = reader.ReadByte();
                LoginSelectWorldCharacterEntry createdCharacter = null;
                if (resultCode == 0)
                {
                    createdCharacter = LoginCharacterStatCodec.DecodeCharacterEntryForCreateNewCharacter(reader);
                }

                profile = new LoginCreateNewCharacterResultProfile
                {
                    ResultCode = resultCode,
                    Payload = Array.Empty<byte>(),
                    CreatedCharacter = createdCharacter
                };
                return true;
            }
            catch (EndOfStreamException)
            {
                error = "CreateNewCharacterResult payload ended before decoding completed.";
                return false;
            }
            catch (IOException)
            {
                error = "CreateNewCharacterResult payload could not be read.";
                return false;
            }
            catch (InvalidDataException ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
