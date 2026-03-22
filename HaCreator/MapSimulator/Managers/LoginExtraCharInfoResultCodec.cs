using MapleLib.PacketLib;
using System;
using System.IO;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class LoginExtraCharInfoResultProfile
    {
        public int AccountId { get; init; }
        public byte ResultFlag { get; init; }
        public bool CanHaveExtraCharacter { get; init; }
    }

    public static class LoginExtraCharInfoResultCodec
    {
        public static bool TryDecode(byte[] data, out LoginExtraCharInfoResultProfile profile, out string error)
        {
            profile = null;
            error = string.Empty;

            if (data == null || data.Length == 0)
            {
                error = "ExtraCharInfoResult payload is empty.";
                return false;
            }

            try
            {
                return TryDecode(new PacketReader(data), out profile, out error);
            }
            catch (EndOfStreamException)
            {
                error = "ExtraCharInfoResult payload ended before decoding completed.";
                return false;
            }
            catch (IOException)
            {
                error = "ExtraCharInfoResult payload could not be read.";
                return false;
            }
        }

        public static bool TryDecode(PacketReader reader, out LoginExtraCharInfoResultProfile profile, out string error)
        {
            profile = null;
            error = string.Empty;
            if (reader == null)
            {
                error = "ExtraCharInfoResult reader is missing.";
                return false;
            }

            try
            {
                int accountId = reader.ReadInt();
                byte resultFlag = reader.ReadByte();
                profile = new LoginExtraCharInfoResultProfile
                {
                    AccountId = accountId,
                    ResultFlag = resultFlag,
                    CanHaveExtraCharacter = resultFlag == 0
                };
                return true;
            }
            catch (EndOfStreamException)
            {
                error = "ExtraCharInfoResult payload ended before decoding completed.";
                return false;
            }
            catch (IOException)
            {
                error = "ExtraCharInfoResult payload could not be read.";
                return false;
            }
        }
    }
}
