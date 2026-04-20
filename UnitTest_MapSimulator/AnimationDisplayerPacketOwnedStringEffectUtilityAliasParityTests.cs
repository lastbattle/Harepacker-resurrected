using HaCreator.MapSimulator;

namespace UnitTest_MapSimulator;

public sealed class AnimationDisplayerPacketOwnedStringEffectUtilityAliasParityTests
{
    [Theory]
    [InlineData("Catch/Success", "Effect/BasicEff.img/Catch/Success")]
    [InlineData("Flame/SquibEffect2", "Effect/BasicEff.img/Flame/SquibEffect2")]
    [InlineData("SquibEffect/0", "Effect/BasicEff.img/Flame/SquibEffect/0")]
    [InlineData("TransformOnLadder/0", "Effect/BasicEff.img/TransformOnLadder/0")]
    [InlineData("BasicEff/CoolHit/cool", "Effect/BasicEff.img/CoolHit/cool")]
    public void NormalizeRemotePacketOwnedStringEffectUol_UtilityAliasPath_NormalizesToBasicEffImagePath(
        string effectPath,
        string expectedUol)
    {
        string normalized = MapSimulator.NormalizeRemotePacketOwnedStringEffectUol(effectPath);

        Assert.Equal(expectedUol, normalized);
    }

    [Theory]
    [InlineData("Catch/Success", true, true)]
    [InlineData("Catch/Fail", true, false)]
    [InlineData("Flame/SquibEffect", false, false)]
    public void TryResolveAnimationDisplayerCatchSuccessFromEffectUol_UtilityAliasPath_ResolvesCatchVariants(
        string effectPath,
        bool expectedResolved,
        bool expectedSuccess)
    {
        bool resolved = MapSimulator.TryResolveAnimationDisplayerCatchSuccessFromEffectUol(
            effectPath,
            out bool success);

        Assert.Equal(expectedResolved, resolved);
        Assert.Equal(expectedSuccess, success);
    }

    [Theory]
    [InlineData("SquibEffect", true, 1)]
    [InlineData("SquibEffect2/0", true, 2)]
    [InlineData("Catch/Fail", false, 0)]
    public void TryResolveAnimationDisplayerSquibVariantFromEffectUol_UtilityAliasPath_ResolvesExpectedVariant(
        string effectPath,
        bool expectedResolved,
        int expectedVariant)
    {
        bool resolved = MapSimulator.TryResolveAnimationDisplayerSquibVariantFromEffectUol(
            effectPath,
            out int variant);

        Assert.Equal(expectedResolved, resolved);
        Assert.Equal(expectedVariant, variant);
    }

    [Theory]
    [InlineData("Transform/0", true, false)]
    [InlineData("TransformOnLadder/0", true, true)]
    [InlineData("Catch/Success", false, false)]
    public void TryResolveAnimationDisplayerTransformedOnLadderFromEffectUol_UtilityAliasPath_ResolvesExpectedLadderFlag(
        string effectPath,
        bool expectedResolved,
        bool expectedOnLadder)
    {
        bool resolved = MapSimulator.TryResolveAnimationDisplayerTransformedOnLadderFromEffectUol(
            effectPath,
            out bool onLadder);

        Assert.Equal(expectedResolved, resolved);
        Assert.Equal(expectedOnLadder, onLadder);
    }
}
