using HaCreator.MapSimulator.Pools;
using Xunit;

namespace UnitTest_MapSimulator
{
    public sealed class PacketOwnedSummonHitParityTests
    {
        [Fact]
        public void SourceSequenceRootPath_ResolvesRelativeSourceTokensAfterAbsoluteStart()
        {
            string[] sourceTokens =
            {
                "Mob/2400011.img/attack1/info/hit/0/source",
                "../1/source",
                "../2/source"
            };

            string resolved = SummonedPool.TryResolvePacketMobAttackGeneralEffectSourceSequenceRootPath(
                sourceTokens,
                "Mob");

            Assert.Equal("Mob/2400011.img/attack1/info/hit", resolved);
        }

        [Fact]
        public void SourceSequenceRootPath_ResolvesRelativeSourceTokensAfterFrameStart()
        {
            string[] sourceTokens =
            {
                "Mob/2400011.img/attack1/info/hit/0",
                "../1/source",
                "../2/source"
            };

            string resolved = SummonedPool.TryResolvePacketMobAttackGeneralEffectSourceSequenceRootPath(
                sourceTokens,
                "Mob");

            Assert.Equal("Mob/2400011.img/attack1/info/hit", resolved);
        }

        [Fact]
        public void SourceSequenceRootPath_ResolvesImageRelativeStartAfterLeadingDotSegments()
        {
            string[] sourceTokens =
            {
                "../2400011.img/attack1/info/hit/0/source",
                "../1/source",
                "../2/source"
            };

            string resolved = SummonedPool.TryResolvePacketMobAttackGeneralEffectSourceSequenceRootPath(
                sourceTokens,
                "Mob");

            Assert.Equal("Mob/2400011.img/attack1/info/hit", resolved);
        }

        [Fact]
        public void SourceSequenceRootPath_ResolvesColonDelimitedRelativeTokensAfterAbsoluteStart()
        {
            string[] sourceTokens =
            {
                "Mob:2400011.img:attack1:info:hit:0:source",
                "..:1:source",
                "..:2:source"
            };

            string resolved = SummonedPool.TryResolvePacketMobAttackGeneralEffectSourceSequenceRootPath(
                sourceTokens,
                "Mob");

            Assert.Equal("Mob/2400011.img/attack1/info/hit", resolved);
        }

        [Fact]
        public void SourceSequenceRootPath_ResolvesSourceAssignmentTokens()
        {
            string[] sourceTokens =
            {
                "source=Mob/2400011.img/attack1/info/hit/0/source",
                "source=../1/source",
                "source=../2/source"
            };

            string resolved = SummonedPool.TryResolvePacketMobAttackGeneralEffectSourceSequenceRootPath(
                sourceTokens,
                "Mob");

            Assert.Equal("Mob/2400011.img/attack1/info/hit", resolved);
        }

        [Fact]
        public void SourceSequenceRootPath_RejectsRelativeFirstTokenWithoutBase()
        {
            string[] sourceTokens =
            {
                "../1/source",
                "../2/source"
            };

            string resolved = SummonedPool.TryResolvePacketMobAttackGeneralEffectSourceSequenceRootPath(
                sourceTokens,
                "Mob");

            Assert.Null(resolved);
        }

        [Fact]
        public void SourceSequenceRootPath_ResolvesAmpersandPackedIndexedAssignmentTokens()
        {
            const string packedSourcePath =
                "source[0]=Mob/2400011.img/attack1/info/hit/0/source&&source[1]=../1/source&&source[2]=../2/source";

            string[] sourceTokens = SummonedPool.EnumeratePacketMobAttackGeneralEffectPathTokens(packedSourcePath);
            string resolved = SummonedPool.TryResolvePacketMobAttackGeneralEffectSourceSequenceRootPath(
                sourceTokens,
                "Mob");

            Assert.Equal("Mob/2400011.img/attack1/info/hit", resolved);
        }

        [Fact]
        public void SourceSequenceRootPath_ResolvesWhitespacePackedAssignmentTokens()
        {
            const string packedSourcePath =
                "source=Mob/2400011.img/attack1/info/hit/0/source source=../1/source source=../2/source";

            string[] sourceTokens = SummonedPool.EnumeratePacketMobAttackGeneralEffectPathTokens(packedSourcePath);
            string resolved = SummonedPool.TryResolvePacketMobAttackGeneralEffectSourceSequenceRootPath(
                sourceTokens,
                "Mob");

            Assert.Equal("Mob/2400011.img/attack1/info/hit", resolved);
        }
    }
}
