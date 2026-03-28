using MapleLib.PacketLib;
using System;
using System.IO;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class LoginCreateNewCharacterResultProfile
    {
        public byte ResultCode { get; init; }
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
                return TryDecode(new PacketReader(data), out profile, out error);
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
