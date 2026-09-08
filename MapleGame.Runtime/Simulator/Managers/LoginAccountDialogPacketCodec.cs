using MapleLib.PacketLib;
using System;
using System.IO;
using System.Text;

using BinaryReader = MapleLib.PacketLib.PacketReader;
namespace HaCreator.MapSimulator.Managers
{
    public sealed class LoginAccountDialogPacketProfile
    {
        public LoginPacketType PacketType { get; init; }
        public string Source { get; init; }
        public byte[] Payload { get; init; } = Array.Empty<byte>();
        public byte? ResultCode { get; init; }
        public byte? SecondaryCode { get; init; }
        public int? AccountId { get; init; }
        public int? CharacterId { get; init; }
        public byte? Gender { get; init; }
        public byte? GradeCode { get; init; }
        public ushort? AccountFlags { get; init; }
        public byte? CountryId { get; init; }
        public string ClubId { get; init; }
        public byte? PurchaseExperience { get; init; }
        public byte? ChatBlockReason { get; init; }
        public long? ChatUnblockFileTime { get; init; }
        public long? RegisterDateFileTime { get; init; }
        public int? CharacterCount { get; init; }
        public byte[] ClientKey { get; init; } = Array.Empty<byte>();
        public string RequestedName { get; init; }
        public string TextValue { get; init; }
    }

    public static class LoginAccountDialogPacketCodec
    {
        public static bool TryDecode(
            LoginPacketType packetType,
            byte[] data,
            out LoginAccountDialogPacketProfile profile,
            out string error)
        {
            profile = null;
            error = null;

            if (!Supports(packetType))
            {
                error = $"{packetType} does not use the login account dialog codec.";
                return false;
            }

            if (data == null || data.Length == 0)
            {
                error = $"{packetType} payload is empty.";
                return false;
            }

            try
            {
                using MemoryStream stream = new(data, writable: false);
                using BinaryReader reader = new(stream);

                int? accountId = null;
                int? characterId = null;
                byte? resultCode = null;
                byte? secondaryCode = null;
                byte? gender = null;
                byte? gradeCode = null;
                ushort? accountFlags = null;
                byte? countryId = null;
                string clubId = null;
                byte? purchaseExperience = null;
                byte? chatBlockReason = null;
                long? chatUnblockFileTime = null;
                long? registerDateFileTime = null;
                int? characterCount = null;
                byte[] clientKey = Array.Empty<byte>();
                string requestedName = null;
                string textValue = null;

                switch (packetType)
                {
                    case LoginPacketType.AccountInfoResult:
                        resultCode = reader.ReadByte();
                        if (resultCode is 0 or 12 or 23)
                        {
                            accountId = reader.ReadInt32();
                            gender = reader.ReadByte();
                            gradeCode = reader.ReadByte();
                            accountFlags = reader.ReadUInt16();
                            countryId = reader.ReadByte();
                            clubId = ReadMapleString(reader);
                            purchaseExperience = reader.ReadByte();
                            chatBlockReason = reader.ReadByte();
                            chatUnblockFileTime = reader.ReadInt64();
                            registerDateFileTime = reader.ReadInt64();
                            characterCount = reader.ReadInt32();
                            clientKey = reader.ReadBytes(8);
                        }

                        textValue = TryReadTrailingMapleString(reader, stream);
                        break;

                    case LoginPacketType.SetAccountResult:
                        resultCode = reader.ReadByte();
                        if (HasRemaining(reader, stream, 1))
                        {
                            secondaryCode = reader.ReadByte();
                        }

                        textValue = TryReadTrailingMapleString(reader, stream);
                        break;

                    case LoginPacketType.EnableSpwResult:
                        secondaryCode = reader.ReadByte();
                        if (HasRemaining(reader, stream, 1))
                        {
                            resultCode = reader.ReadByte();
                        }

                        textValue = TryReadTrailingMapleString(reader, stream);
                        break;

                    case LoginPacketType.DeleteCharacterResult:
                        characterId = reader.ReadInt32();
                        resultCode = reader.ReadByte();
                        textValue = TryReadTrailingMapleString(reader, stream);
                        break;

                    case LoginPacketType.CreateNewCharacterResult:
                        resultCode = reader.ReadByte();
                        textValue = TryReadTrailingMapleString(reader, stream);
                        break;

                    case LoginPacketType.CheckDuplicatedIdResult:
                        requestedName = ReadMapleString(reader);
                        resultCode = reader.ReadByte();
                        textValue = TryReadTrailingMapleString(reader, stream);
                        break;

                    default:
                        resultCode = reader.ReadByte();
                        if (HasRemaining(reader, stream, 1))
                        {
                            secondaryCode = reader.ReadByte();
                        }

                        if (HasRemaining(reader, stream, sizeof(int)) &&
                            (packetType == LoginPacketType.SetAccountResult || packetType == LoginPacketType.EnableSpwResult))
                        {
                            accountId = reader.ReadInt32();
                        }

                        textValue = TryReadTrailingMapleString(reader, stream);
                        break;
                }

                profile = new LoginAccountDialogPacketProfile
                {
                    PacketType = packetType,
                    Payload = (byte[])data.Clone(),
                    ResultCode = resultCode,
                    SecondaryCode = secondaryCode,
                    AccountId = accountId,
                    CharacterId = characterId,
                    Gender = gender,
                    GradeCode = gradeCode,
                    AccountFlags = accountFlags,
                    CountryId = countryId,
                    ClubId = clubId,
                    PurchaseExperience = purchaseExperience,
                    ChatBlockReason = chatBlockReason,
                    ChatUnblockFileTime = chatUnblockFileTime,
                    RegisterDateFileTime = registerDateFileTime,
                    CharacterCount = characterCount,
                    ClientKey = clientKey,
                    RequestedName = requestedName,
                    TextValue = textValue,
                };
                return true;
            }
            catch (EndOfStreamException)
            {
                error = $"{packetType} payload ended before decoding completed.";
                return false;
            }
            catch (IOException)
            {
                error = $"{packetType} payload could not be read.";
                return false;
            }
        }

        public static bool Supports(LoginPacketType packetType)
        {
            return packetType is LoginPacketType.AccountInfoResult
                or LoginPacketType.SetAccountResult
                or LoginPacketType.ConfirmEulaResult
                or LoginPacketType.CheckPinCodeResult
                or LoginPacketType.UpdatePinCodeResult
                or LoginPacketType.EnableSpwResult
                or LoginPacketType.CheckSpwResult
                or LoginPacketType.CheckDuplicatedIdResult
                or LoginPacketType.CreateNewCharacterResult
                or LoginPacketType.DeleteCharacterResult;
        }

        private static bool HasRemaining(BinaryReader reader, Stream stream, int byteCount)
        {
            return stream.Length - reader.BaseStream.Position >= byteCount;
        }

        private static string TryReadTrailingMapleString(BinaryReader reader, Stream stream)
        {
            if (!HasRemaining(reader, stream, sizeof(short)))
            {
                return null;
            }

            long startPosition = reader.BaseStream.Position;
            short length = reader.ReadInt16();
            if (length < 0 || !HasRemaining(reader, stream, length))
            {
                reader.BaseStream.Position = startPosition;
                return null;
            }

            return length == 0 ? string.Empty : Encoding.ASCII.GetString(reader.ReadBytes(length));
        }

        private static string ReadMapleString(BinaryReader reader)
        {
            short length = reader.ReadInt16();
            if (length <= 0)
            {
                return string.Empty;
            }

            return Encoding.ASCII.GetString(reader.ReadBytes(length));
        }
    }
}
