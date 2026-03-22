using HaCreator.MapSimulator.Managers;
using System.IO;
using Xunit;

namespace UnitTest_MapSimulator
{
    public class LoginExtraCharInfoResultCodecTests
    {
        [Fact]
        public void TryDecode_DecodesAccountAndEligibilityFlag()
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(123456789);
            writer.Write((byte)0);
            writer.Flush();

            bool decoded = LoginExtraCharInfoResultCodec.TryDecode(stream.ToArray(), out LoginExtraCharInfoResultProfile profile, out string error);

            Assert.True(decoded, error);
            Assert.Equal(123456789, profile.AccountId);
            Assert.Equal(0, profile.ResultFlag);
            Assert.True(profile.CanHaveExtraCharacter);
        }
    }
}
