using MapleLib.PacketLib;
using System;
using System.IO;
using System.Net;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class LoginSelectCharacterResultProfile
    {
        public byte ResultCode { get; init; }
        public byte ResponseCode { get; init; }
        public byte[] Payload { get; init; } = Array.Empty<byte>();
        public IPAddress ServerAddress { get; init; }
        public ushort? Port { get; init; }
        public int? CharacterId { get; init; }
        public byte? AuthenCode { get; init; }
        public int? PremiumArgument { get; init; }

        public bool IsSuccess =>
            LoginSelectCharacterResultCodec.IsSuccess(ResultCode, ResponseCode);

        public bool HasCompleteConnectPayload =>
            ServerAddress != null &&
            Port.HasValue &&
            CharacterId.HasValue &&
            AuthenCode.HasValue &&
            PremiumArgument.HasValue;

        public bool RequiresWebsiteHandoff =>
            LoginSelectCharacterResultCodec.RequiresWebsiteHandoff(ResultCode);

        public bool ReturnsToTitle =>
            LoginSelectCharacterResultCodec.ShouldReturnToTitle(ResultCode, ResponseCode);

        public int? NoticeTextIndex =>
            LoginSelectCharacterResultCodec.ResolveFailureNoticeTextIndex(ResultCode, ResponseCode);

        public string EndpointText =>
            ServerAddress != null && Port.HasValue
                ? $"{ServerAddress}:{Port.Value}"
                : null;
    }

    public static class LoginSelectCharacterResultCodec
    {
        public static bool TryDecode(byte[] data, out LoginSelectCharacterResultProfile profile, out string error)
        {
            profile = null;
            error = string.Empty;

            if (data == null || data.Length == 0)
            {
                error = "SelectCharacterResult payload is empty.";
                return false;
            }

            try
            {
                bool decoded = TryDecode(new PacketReader(data), out profile, out error);
                if (decoded && profile != null)
                {
                    profile = new LoginSelectCharacterResultProfile
                    {
                        ResultCode = profile.ResultCode,
                        ResponseCode = profile.ResponseCode,
                        Payload = (byte[])data.Clone(),
                        ServerAddress = profile.ServerAddress,
                        Port = profile.Port,
                        CharacterId = profile.CharacterId,
                        AuthenCode = profile.AuthenCode,
                        PremiumArgument = profile.PremiumArgument
                    };
                }

                return decoded;
            }
            catch (EndOfStreamException)
            {
                error = "SelectCharacterResult payload ended before decoding completed.";
                return false;
            }
            catch (IOException)
            {
                error = "SelectCharacterResult payload could not be read.";
                return false;
            }
        }

        public static bool TryDecode(PacketReader reader, out LoginSelectCharacterResultProfile profile, out string error)
        {
            profile = null;
            error = string.Empty;
            if (reader == null)
            {
                error = "SelectCharacterResult reader is missing.";
                return false;
            }

            try
            {
                byte resultCode = reader.ReadByte();
                byte responseCode = reader.ReadByte();
                IPAddress serverAddress = null;
                ushort? port = null;
                int? characterId = null;
                byte? authenCode = null;
                int? premiumArgument = null;

                if (IsSuccess(resultCode, responseCode))
                {
                    serverAddress = new IPAddress(reader.ReadBytes(4));
                    port = unchecked((ushort)reader.ReadShort());
                    characterId = reader.ReadInt();
                    authenCode = reader.ReadByte();
                    premiumArgument = reader.ReadInt();
                }

                profile = new LoginSelectCharacterResultProfile
                {
                    ResultCode = resultCode,
                    ResponseCode = responseCode,
                    Payload = Array.Empty<byte>(),
                    ServerAddress = serverAddress,
                    Port = port,
                    CharacterId = characterId,
                    AuthenCode = authenCode,
                    PremiumArgument = premiumArgument
                };
                return true;
            }
            catch (EndOfStreamException)
            {
                error = "SelectCharacterResult payload ended before decoding completed.";
                return false;
            }
            catch (IOException)
            {
                error = "SelectCharacterResult payload could not be read.";
                return false;
            }
        }

        public static bool IsSuccess(byte resultCode, byte responseCode)
        {
            return resultCode == 0 ||
                   resultCode == 23 ||
                   (resultCode == 12 && (responseCode == 11 || responseCode == 13));
        }

        public static bool RequiresWebsiteHandoff(byte resultCode)
        {
            return resultCode is 14 or 15;
        }

        public static bool ShouldReturnToTitle(byte resultCode, byte responseCode)
        {
            if (IsSuccess(resultCode, responseCode) || RequiresWebsiteHandoff(resultCode))
            {
                return false;
            }

            return resultCode == 7 ||
                   (resultCode == 12 && responseCode is 1 or 2 or 3 or 19 or 25 or 27 or 28);
        }

        public static int? ResolveFailureNoticeTextIndex(byte resultCode, byte responseCode)
        {
            if (IsSuccess(resultCode, responseCode))
            {
                return null;
            }

            if (resultCode == 12)
            {
                return responseCode switch
                {
                    1 => 28,
                    2 => 29,
                    3 => 30,
                    19 => 25,
                    25 => 31,
                    27 => 56,
                    28 => 62,
                    _ => 15,
                };
            }

            return resultCode switch
            {
                255 or 6 or 8 or 9 => 15,
                2 or 3 => 16,
                4 => 3,
                5 => 20,
                7 => 17,
                10 => 19,
                11 => 14,
                13 => 21,
                14 => 27,
                15 => 26,
                16 or 21 => 33,
                17 => 27,
                25 => 40,
                _ => 15,
            };
        }
    }
}
