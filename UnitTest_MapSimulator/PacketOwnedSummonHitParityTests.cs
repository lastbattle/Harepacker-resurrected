using HaCreator.MapSimulator.Pools;
using Xunit;

namespace UnitTest_MapSimulator;

public sealed class PacketOwnedSummonHitParityTests
{
    [Fact]
    public void EnumeratePathTokens_PreservesSourceAliasAssignmentToken()
    {
        string[] tokens = SummonedPool.EnumeratePacketMobAttackGeneralEffectPathTokens("source=3");

        Assert.Contains("source:3", tokens);
        Assert.DoesNotContain("3", tokens);
    }

    [Fact]
    public void EnumeratePathTokens_NormalizesEscapedAndPercentEncodedSeparators()
    {
        string[] escapedTokens = SummonedPool.EnumeratePacketMobAttackGeneralEffectPathTokens(
            @"Mob\/2400017.img\/attack1\/info\/hit\/0");
        string[] encodedTokens = SummonedPool.EnumeratePacketMobAttackGeneralEffectPathTokens(
            "Mob%2F2400017.img%2Fattack1%2Finfo%2Fhit%2F0");

        Assert.Contains("Mob/2400017.img/attack1/info/hit/0", escapedTokens);
        Assert.Contains("Mob/2400017.img/attack1/info/hit/0", encodedTokens);
    }

    [Fact]
    public void ResolveSequenceRootPath_HandlesEscapedSignedSourceAliasOffsets()
    {
        string rootPath = SummonedPool.TryResolvePacketMobAttackGeneralEffectSourceSequenceRootPath(
            new[]
            {
                "Mob/2400017.img/attack1/3/source",
                @"source\:-1",
                @"source\:+1"
            },
            "Mob");

        Assert.Equal("Mob/2400017.img/attack1", rootPath);
    }

    [Fact]
    public void EnumerateCandidateUols_NormalizesPercentEncodedAbsoluteToken()
    {
        string[] candidates = SummonedPool.EnumeratePacketMobAttackGeneralEffectCandidateUols(
            "Mob%2F2400017.img%2Fattack1%2Finfo%2Fhit%2F0",
            "2400110",
            "attack1");

        Assert.Contains("Mob/2400017.img/attack1/info/hit/0", candidates);
    }

    [Theory]
    [InlineData("./source(-1)", "Mob/2400011.img/attack1/info/hit/0/source")]
    [InlineData("./source/-1", "Mob/2400011.img/attack1/info/hit/0/source")]
    [InlineData("./source.-1", "Mob/2400011.img/attack1/info/hit/0/source")]
    public void RelativeSourcePath_ResolvesSignedSiblingOffsetAliases(
        string sourceAliasToken,
        string expectedResolvedPath)
    {
        bool resolved = SummonedPool.TryResolvePacketMobAttackGeneralEffectRelativeSourcePath(
            "Mob/2400011.img/attack1/info/hit/1/source",
            sourceAliasToken,
            "Mob",
            out string resolvedPath);

        Assert.True(resolved);
        Assert.Equal(expectedResolvedPath, resolvedPath);
    }

    [Theory]
    [InlineData("./source(1)", "Mob/2400011.img/attack1/info/hit/1/source")]
    [InlineData("./1/source", "Mob/2400011.img/attack1/info/hit/1/source")]
    [InlineData("../1/source", "Mob/2400011.img/attack1/info/hit/1/source")]
    public void RelativeSourcePath_KeepsExistingUnsignedAndLeafRelativeFallbacks(
        string sourceAliasToken,
        string expectedResolvedPath)
    {
        bool resolved = SummonedPool.TryResolvePacketMobAttackGeneralEffectRelativeSourcePath(
            "Mob/2400011.img/attack1/info/hit/2/source",
            sourceAliasToken,
            "Mob",
            out string resolvedPath);

        Assert.True(resolved);
        Assert.Equal(expectedResolvedPath, resolvedPath);
    }
}
