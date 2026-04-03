using MapleLib.PacketLib;
using System;
using System.IO;
using System.Net;

namespace HaCreator.MapSimulator.Managers
{
    public enum LoginSelectCharacterByVacSuccessBranch
    {
        None = 0,
        DirectResult0 = 1,
        AlternateAuthenticatedResult12Secondary11 = 2,
        AlternateAuthenticatedResult12Secondary13 = 3,
        AlternateResult23 = 4,
    }

    public sealed class LoginSelectCharacterByVacResultProfile
    {
        public byte ResultCode { get; init; }
        public byte SecondaryCode { get; init; }
        public byte[] Payload { get; init; } = Array.Empty<byte>();
        public IPAddress ServerAddress { get; init; }
        public ushort? Port { get; init; }
        public int? CharacterId { get; init; }
        public byte? AuthenCode { get; init; }
        public int? PremiumArgument { get; init; }
        public bool HasCompleteConnectPayload =>
            ServerAddress != null &&
            Port.HasValue &&
            CharacterId.HasValue &&
            AuthenCode.HasValue &&
            PremiumArgument.HasValue;

        public bool IsConnectSuccess =>
            LoginSelectCharacterByVacResultCodec.IsConnectSuccess(ResultCode, SecondaryCode);

        public LoginSelectCharacterByVacSuccessBranch SuccessBranch =>
            LoginSelectCharacterByVacResultCodec.ResolveSuccessBranch(ResultCode, SecondaryCode);

        public bool UsesDirectSuccessBranch => SuccessBranch == LoginSelectCharacterByVacSuccessBranch.DirectResult0;
        public bool UsesAlternateAuthenticatedBranch =>
            SuccessBranch is LoginSelectCharacterByVacSuccessBranch.AlternateAuthenticatedResult12Secondary11 or
                             LoginSelectCharacterByVacSuccessBranch.AlternateAuthenticatedResult12Secondary13;
        public bool UsesAlternateSuccessBranch => SuccessBranch == LoginSelectCharacterByVacSuccessBranch.AlternateResult23;
        public bool RequiresWebsiteHandoff => LoginSelectCharacterByVacResultCodec.RequiresWebsiteHandoff(ResultCode);
        public bool ReturnsToTitle => LoginSelectCharacterByVacResultCodec.ShouldReturnToTitle(ResultCode, SecondaryCode);
        public int? NoticeTextIndex => LoginSelectCharacterByVacResultCodec.ResolveFailureNoticeTextIndex(ResultCode, SecondaryCode);

        public string EndpointText =>
            ServerAddress != null && Port.HasValue
                ? $"{ServerAddress}:{Port.Value}"
                : null;
    }

    public static class LoginSelectCharacterByVacResultCodec
    {
        public static bool TryDecode(byte[] data, out LoginSelectCharacterByVacResultProfile profile, out string error)
        {
            profile = null;
            error = string.Empty;

            if (data == null || data.Length == 0)
            {
                error = "SelectCharacterByVACResult payload is empty.";
                return false;
            }

            try
            {
                bool decoded = TryDecode(new PacketReader(data), out profile, out error);
                if (decoded && profile != null)
                {
                    profile = new LoginSelectCharacterByVacResultProfile
                    {
                        ResultCode = profile.ResultCode,
                        SecondaryCode = profile.SecondaryCode,
                        Payload = (byte[])data.Clone(),
                        ServerAddress = profile.ServerAddress,
                        Port = profile.Port,
                        CharacterId = profile.CharacterId,
                        AuthenCode = profile.AuthenCode,
                        PremiumArgument = profile.PremiumArgument,
                    };
                }

                return decoded;
            }
            catch (EndOfStreamException)
            {
                error = "SelectCharacterByVACResult payload ended before decoding completed.";
                return false;
            }
            catch (IOException)
            {
                error = "SelectCharacterByVACResult payload could not be read.";
                return false;
            }
        }

        public static bool TryDecode(PacketReader reader, out LoginSelectCharacterByVacResultProfile profile, out string error)
        {
            profile = null;
            error = string.Empty;
            if (reader == null)
            {
                error = "SelectCharacterByVACResult reader is missing.";
                return false;
            }

            try
            {
                byte resultCode = reader.ReadByte();
                byte secondaryCode = reader.ReadByte();
                IPAddress serverAddress = null;
                ushort? port = null;
                int? characterId = null;
                byte? authenCode = null;
                int? premiumArgument = null;

                if (IsConnectSuccess(resultCode, secondaryCode))
                {
                    serverAddress = new IPAddress(reader.ReadBytes(4));
                    port = unchecked((ushort)reader.ReadShort());
                    characterId = reader.ReadInt();
                    authenCode = reader.ReadByte();
                    premiumArgument = reader.ReadInt();
                }

                profile = new LoginSelectCharacterByVacResultProfile
                {
                    ResultCode = resultCode,
                    SecondaryCode = secondaryCode,
                    Payload = Array.Empty<byte>(),
                    ServerAddress = serverAddress,
                    Port = port,
                    CharacterId = characterId,
                    AuthenCode = authenCode,
                    PremiumArgument = premiumArgument,
                };
                return true;
            }
            catch (EndOfStreamException)
            {
                error = "SelectCharacterByVACResult payload ended before decoding completed.";
                return false;
            }
            catch (IOException)
            {
                error = "SelectCharacterByVACResult payload could not be read.";
                return false;
            }
        }

        public static bool IsConnectSuccess(byte resultCode, byte secondaryCode)
        {
            return resultCode == 0 ||
                   resultCode == 23 ||
                   (resultCode == 12 && (secondaryCode == 11 || secondaryCode == 13));
        }

        public static LoginSelectCharacterByVacSuccessBranch ResolveSuccessBranch(byte resultCode, byte secondaryCode)
        {
            if (resultCode == 0)
            {
                return LoginSelectCharacterByVacSuccessBranch.DirectResult0;
            }

            if (resultCode == 23)
            {
                return LoginSelectCharacterByVacSuccessBranch.AlternateResult23;
            }

            if (resultCode == 12)
            {
                return secondaryCode switch
                {
                    11 => LoginSelectCharacterByVacSuccessBranch.AlternateAuthenticatedResult12Secondary11,
                    13 => LoginSelectCharacterByVacSuccessBranch.AlternateAuthenticatedResult12Secondary13,
                    _ => LoginSelectCharacterByVacSuccessBranch.None,
                };
            }

            return LoginSelectCharacterByVacSuccessBranch.None;
        }

        public static bool RequiresWebsiteHandoff(byte resultCode)
        {
            return resultCode is 14 or 15;
        }

        public static bool ShouldReturnToTitle(byte resultCode, byte secondaryCode)
        {
            if (IsConnectSuccess(resultCode, secondaryCode) || RequiresWebsiteHandoff(resultCode))
            {
                return false;
            }

            // Client evidence: CLogin::OnSelectCharacterByVACResult (0x5de670) only calls
            // GotoTitle for result 7 and result 12 secondaries 1, 2, 3, 19, 25, 27, and 28.
            return resultCode == 7 ||
                   (resultCode == 12 && secondaryCode is 1 or 2 or 3 or 19 or 25 or 27 or 28);
        }

        public static int? ResolveFailureNoticeTextIndex(byte resultCode, byte secondaryCode)
        {
            if (IsConnectSuccess(resultCode, secondaryCode))
            {
                return null;
            }

            if (resultCode == 12)
            {
                // Client evidence: CLogin::OnSelectCharacterByVACResult (0x5de670)
                // routes the alternate authenticated failure family through the
                // Login.img/Notice/text indices below.
                return secondaryCode switch
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
