using HaCreator.MapSimulator.Managers;
using System.Text;

namespace UnitTest_MapSimulator
{
    public class LoginAccountDialogPacketCodecTests
    {
        [Fact]
        public void TryDecode_AccountInfoResult_ExtractsAccountPayloadFields()
        {
            byte[] payload = BuildAccountInfoPayload();

            bool decoded = LoginAccountDialogPacketCodec.TryDecode(
                LoginPacketType.AccountInfoResult,
                payload,
                out LoginAccountDialogPacketProfile profile,
                out string error);

            Assert.True(decoded, error);
            Assert.NotNull(profile);
            Assert.Equal(LoginPacketType.AccountInfoResult, profile.PacketType);
            Assert.Equal((byte)0, profile.ResultCode);
            Assert.Equal(123456789, profile.AccountId);
            Assert.Equal((byte)1, profile.Gender);
            Assert.Equal((byte)5, profile.GradeCode);
            Assert.Equal((ushort)0x0137, profile.AccountFlags);
            Assert.Equal((byte)8, profile.CountryId);
            Assert.Equal("club-maple", profile.ClubId);
            Assert.Equal((byte)2, profile.PurchaseExperience);
            Assert.Equal((byte)4, profile.ChatBlockReason);
            Assert.Equal(4, profile.CharacterCount);
            Assert.Equal("Session account hydrated from packet", profile.TextValue);
            Assert.Equal("0102030405060708", Convert.ToHexString(profile.ClientKey));
        }

        [Fact]
        public void BuildDetailBlock_IncludesDecodedAccountPayloadSummary()
        {
            var registerDate = new DateTime(2024, 03, 01, 12, 30, 00, DateTimeKind.Utc);
            var unblockDate = new DateTime(2024, 03, 05, 08, 15, 00, DateTimeKind.Utc);
            LoginAccountDialogPacketProfile profile = new()
            {
                PacketType = LoginPacketType.AccountInfoResult,
                ResultCode = 0,
                SecondaryCode = 1,
                AccountId = 42,
                CharacterId = 99,
                Gender = 0,
                GradeCode = 3,
                AccountFlags = 0x00AF,
                CountryId = 1,
                ClubId = "alpha",
                PurchaseExperience = 7,
                ChatBlockReason = 2,
                ChatUnblockFileTime = unblockDate.ToFileTimeUtc(),
                RegisterDateFileTime = registerDate.ToFileTimeUtc(),
                CharacterCount = 6,
                ClientKey = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD },
            };

            string detailBlock = LoginAccountDialogPacketProfileFormatter.BuildDetailBlock(profile);

            Assert.Contains("Result code: 0", detailBlock);
            Assert.Contains("Secondary code: 1", detailBlock);
            Assert.Contains("Account id: 42", detailBlock);
            Assert.Contains("Character id: 99", detailBlock);
            Assert.Contains("Gender: Male (0)", detailBlock);
            Assert.Contains("Grade code: 3", detailBlock);
            Assert.Contains("Account flags: 0x00AF", detailBlock);
            Assert.Contains("Country id: 1", detailBlock);
            Assert.Contains("Club id: alpha", detailBlock);
            Assert.Contains("Purchase exp: 7", detailBlock);
            Assert.Contains("Chat block reason: 2", detailBlock);
            Assert.Contains("Chat unblock: 2024-03-05 08:15:00 UTC", detailBlock);
            Assert.Contains("Register date: 2024-03-01 12:30:00 UTC", detailBlock);
            Assert.Contains("Character count: 6", detailBlock);
            Assert.Contains("Client key: AABBCCDD", detailBlock);
        }

        private static byte[] BuildAccountInfoPayload()
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.ASCII, leaveOpen: true);
            writer.Write((byte)0);
            writer.Write(123456789);
            writer.Write((byte)1);
            writer.Write((byte)5);
            writer.Write((ushort)0x0137);
            writer.Write((byte)8);
            WriteMapleString(writer, "club-maple");
            writer.Write((byte)2);
            writer.Write((byte)4);
            writer.Write(new DateTime(2024, 01, 11, 03, 00, 00, DateTimeKind.Utc).ToFileTimeUtc());
            writer.Write(new DateTime(2023, 12, 25, 18, 45, 00, DateTimeKind.Utc).ToFileTimeUtc());
            writer.Write(4);
            writer.Write(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            WriteMapleString(writer, "Session account hydrated from packet");
            writer.Flush();
            return stream.ToArray();
        }

        private static void WriteMapleString(BinaryWriter writer, string value)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(value ?? string.Empty);
            writer.Write((short)bytes.Length);
            writer.Write(bytes);
        }
    }
}
