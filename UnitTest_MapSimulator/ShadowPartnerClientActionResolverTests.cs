using System;
using System.Linq;
using HaCreator.MapSimulator.Character;
using Xunit;

namespace UnitTest_MapSimulator;

public class ShadowPartnerClientActionResolverTests
{
    [Fact]
    public void EnumerateClientActionAliases_GhostFly_PrefersStandFamilyBeforeFlyFallback()
    {
        string[] aliases = ShadowPartnerClientActionResolver
            .EnumerateClientActionAliases("ghostfly")
            .ToArray();

        Assert.Equal(new[] { "stand1", "stand2", "fly", "jump" }, aliases);
    }

    [Fact]
    public void EnumerateClientActionAliases_Fly2Skill_UsesSharedFloatFallbackFamily()
    {
        string[] aliases = ShadowPartnerClientActionResolver
            .EnumerateClientActionAliases("fly2Skill")
            .ToArray();

        Assert.Equal(new[] { "stand1", "stand2", "fly", "jump" }, aliases);
    }

    [Fact]
    public void EnumerateClientActionAliases_GhostProne_MapsToProne()
    {
        string[] aliases = ShadowPartnerClientActionResolver
            .EnumerateClientActionAliases("ghostprone")
            .ToArray();

        Assert.Equal(new[] { "prone" }, aliases);
    }

    [Fact]
    public void EnumerateClientMappedCandidates_FloatingState_KeepsClientStandFirstOrdering()
    {
        string[] aliases = ShadowPartnerClientActionResolver
            .EnumerateClientMappedCandidates("fly2Move", PlayerState.Flying, "stand1")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(new[] { "stand1", "stand2", "fly", "jump" }, aliases);
    }

    [Fact]
    public void EnumerateClientActionAliases_AlertVariant_CollapsesToAlert()
    {
        string[] aliases = ShadowPartnerClientActionResolver
            .EnumerateClientActionAliases("alert3")
            .ToArray();

        Assert.Equal(new[] { "alert" }, aliases);
    }
}
