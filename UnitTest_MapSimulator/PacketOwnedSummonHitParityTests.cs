using HaCreator.MapSimulator.Pools;

namespace UnitTest_MapSimulator;

public sealed class PacketOwnedSummonHitParityTests
{
    [Theory]
    [InlineData("source=> 3", "Mob/2400011.img/attack1/3/source")]
    [InlineData("source-> 2", "Mob/2400011.img/attack1/2/source")]
    [InlineData("source/ 1", "Mob/2400011.img/attack1/1/source")]
    [InlineData("source( 2 )", "Mob/2400011.img/attack1/2/source")]
    [InlineData("source< 4 >", "Mob/2400011.img/attack1/4/source")]
    public void ShouldSeedSingleTokenAliasRoots_WhenAliasUsesWhitespaceDelimitedTrailingForms(
        string hitEffectPath,
        string expectedRoot)
    {
        string[] candidates = SummonedPool.EnumeratePacketMobAttackGeneralEffectCandidateUols(
            hitEffectPath,
            "2400011",
            "attack1");

        Assert.Contains(expectedRoot, candidates);
    }
}
