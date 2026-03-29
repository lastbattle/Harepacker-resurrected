using System;
using System.IO;
using System.Text;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class LoginCheckPasswordResultProfile
    {
        public byte[] Payload { get; init; } = Array.Empty<byte>();
        public byte ResultCode { get; init; }
        public byte AccountBootstrapMode { get; init; }
        public int UnknownValue { get; init; }
        public int? AccountId { get; init; }
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
        public byte? SecurityCommand { get; init; }
        public byte? UnknownFlag { get; init; }
        public byte[] ClientKey { get; init; } = Array.Empty<byte>();

        public bool IsSuccess => ResultCode is 0 or 12;
        public bool IsLicenseResult => ResultCode == 23;
        public bool HasAccountInfo => (IsSuccess || IsLicenseResult) && AccountBootstrapMode is 0 or 1;
        public bool RequiresAccountRegistration => (IsSuccess || IsLicenseResult) && AccountBootstrapMode is 2 or 3;
    }

    public static class LoginCheckPasswordResultCodec
    {
        public static bool TryDecode(
            byte[] data,
            out LoginCheckPasswordResultProfile profile,
            out string error)
        {
            profile = null;
            error = null;

            if (data == null || data.Length == 0)
            {
                error = "CheckPasswordResult payload is empty.";
                return false;
            }

            try
            {
                using MemoryStream stream = new(data, writable: false);
                using BinaryReader reader = new(stream);

                byte resultCode = reader.ReadByte();
                byte accountBootstrapMode = reader.ReadByte();
                int unknownValue = reader.ReadInt32();
                int? accountId = null;
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
                byte? securityCommand = null;
                byte? unknownFlag = null;
                byte[] clientKey = Array.Empty<byte>();

                if ((resultCode is 0 or 12 or 23) && accountBootstrapMode is 0 or 1)
                {
                    if (resultCode != 23)
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
                        securityCommand = reader.ReadByte();
                        unknownFlag = reader.ReadByte();
                        clientKey = reader.ReadBytes(8);
                        if (clientKey.Length != 8)
                        {
                            throw new EndOfStreamException();
                        }
                    }
                }

                profile = new LoginCheckPasswordResultProfile
                {
                    Payload = (byte[])data.Clone(),
                    ResultCode = resultCode,
                    AccountBootstrapMode = accountBootstrapMode,
                    UnknownValue = unknownValue,
                    AccountId = accountId,
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
                    SecurityCommand = securityCommand,
                    UnknownFlag = unknownFlag,
                    ClientKey = clientKey,
                };
                return true;
            }
            catch (EndOfStreamException)
            {
                error = "CheckPasswordResult payload ended before decoding completed.";
                return false;
            }
            catch (IOException)
            {
                error = "CheckPasswordResult payload could not be read.";
                return false;
            }
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
