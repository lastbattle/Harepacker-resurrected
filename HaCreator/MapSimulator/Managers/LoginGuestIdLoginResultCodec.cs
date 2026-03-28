using System;
using System.IO;
using System.Text;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class LoginGuestIdLoginResultProfile
    {
        public byte[] Payload { get; init; } = Array.Empty<byte>();
        public byte ResultCode { get; init; }
        public byte RegistrationStatusId { get; init; }
        public int? AccountId { get; init; }
        public byte? Gender { get; init; }
        public byte? GradeCode { get; init; }
        public byte? CountryId { get; init; }
        public byte? UnknownFlag { get; init; }
        public string ClubId { get; init; }
        public byte? PurchaseExperience { get; init; }
        public byte? ChatBlockReason { get; init; }
        public long? ChatUnblockFileTime { get; init; }
        public long? RegisterDateFileTime { get; init; }
        public int? CharacterCount { get; init; }
        public string GuestRegistrationUrl { get; init; }
    }

    public static class LoginGuestIdLoginResultCodec
    {
        public static bool TryDecode(
            byte[] data,
            out LoginGuestIdLoginResultProfile profile,
            out string error)
        {
            profile = null;
            error = null;

            if (data == null || data.Length == 0)
            {
                error = "GuestIdLoginResult payload is empty.";
                return false;
            }

            try
            {
                using MemoryStream stream = new(data, writable: false);
                using BinaryReader reader = new(stream);

                byte resultCode = reader.ReadByte();
                byte registrationStatusId = reader.ReadByte();
                int? accountId = null;
                byte? gender = null;
                byte? gradeCode = null;
                byte? countryId = null;
                byte? unknownFlag = null;
                string clubId = null;
                byte? purchaseExperience = null;
                byte? chatBlockReason = null;
                long? chatUnblockFileTime = null;
                long? registerDateFileTime = null;
                int? characterCount = null;
                string guestRegistrationUrl = null;

                if (resultCode is 0 or 12 or 23)
                {
                    switch (registrationStatusId)
                    {
                        case 0:
                        case 1:
                            accountId = reader.ReadInt32();
                            gender = reader.ReadByte();
                            gradeCode = reader.ReadByte();
                            countryId = reader.ReadByte();
                            unknownFlag = reader.ReadByte();
                            clubId = ReadMapleString(reader);
                            purchaseExperience = reader.ReadByte();
                            chatBlockReason = reader.ReadByte();
                            chatUnblockFileTime = reader.ReadInt64();
                            registerDateFileTime = reader.ReadInt64();
                            characterCount = reader.ReadInt32();
                            guestRegistrationUrl = ReadMapleString(reader);
                            break;

                        case 2:
                        case 3:
                            break;
                    }
                }

                profile = new LoginGuestIdLoginResultProfile
                {
                    Payload = (byte[])data.Clone(),
                    ResultCode = resultCode,
                    RegistrationStatusId = registrationStatusId,
                    AccountId = accountId,
                    Gender = gender,
                    GradeCode = gradeCode,
                    CountryId = countryId,
                    UnknownFlag = unknownFlag,
                    ClubId = clubId,
                    PurchaseExperience = purchaseExperience,
                    ChatBlockReason = chatBlockReason,
                    ChatUnblockFileTime = chatUnblockFileTime,
                    RegisterDateFileTime = registerDateFileTime,
                    CharacterCount = characterCount,
                    GuestRegistrationUrl = guestRegistrationUrl,
                };
                return true;
            }
            catch (EndOfStreamException)
            {
                error = "GuestIdLoginResult payload ended before decoding completed.";
                return false;
            }
            catch (IOException)
            {
                error = "GuestIdLoginResult payload could not be read.";
                return false;
            }
        }

        public static bool IsSuccessCode(byte resultCode)
        {
            return resultCode is 0 or 12 or 23;
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
