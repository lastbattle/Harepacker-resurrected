using MapleLib.PacketLib;

namespace MapleLib.Tests.PacketLib
{
    public class MapleHandshakePolicyTests
    {
        [Fact]
        public void GlobalV95_AllowsVersion95()
        {
            bool ok = MapleHandshakePolicy.GlobalV95.TryResolveSessionVersion(95, out short version, out string error);

            Assert.True(ok);
            Assert.Equal(95, version);
            Assert.Null(error);
        }

        [Fact]
        public void StrictPolicy_RejectsMismatchedVersion()
        {
            MapleHandshakePolicy policy = new MapleHandshakePolicy(95, rejectMismatchedVersions: true);

            bool ok = policy.TryResolveSessionVersion(94, out short version, out string error);

            Assert.False(ok);
            Assert.Equal(94, version);
            Assert.NotNull(error);
        }

        [Fact]
        public void NonStrictPolicy_NormalizesToRequiredVersion()
        {
            MapleHandshakePolicy policy = new MapleHandshakePolicy(95, rejectMismatchedVersions: false);

            bool ok = policy.TryResolveSessionVersion(94, out short version, out string error);

            Assert.True(ok);
            Assert.Equal(95, version);
            Assert.Null(error);
        }

        [Fact]
        public void CreateCrypto_ClonesIv()
        {
            byte[] iv = { 1, 2, 3, 4 };

            var crypto = MapleHandshakePolicy.GlobalV95.CreateCrypto(iv, 95);
            iv[0] = 9;

            Assert.Equal(new byte[] { 1, 2, 3, 4 }, crypto.IV);
        }
    }
}
