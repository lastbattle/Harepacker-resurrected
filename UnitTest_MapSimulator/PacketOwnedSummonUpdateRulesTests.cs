using HaCreator.MapSimulator.Loaders;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace UnitTest_MapSimulator;

public sealed class PacketOwnedSummonUpdateRulesTests
{
    [Fact]
    public void BuildAttackInfoMetadata_UsesAttackFrameHitFlagsWhenInfoHitFlagsAreAbsent()
    {
        WzSubProperty infoNode = CreateSubProperty("info");
        WzSubProperty attackStateNode = CreateSubProperty(
            "attack1",
            CreateSubProperty("1", CreateSubProperty("hit", new WzIntProperty("attach", 1))),
            CreateSubProperty("2", CreateSubProperty("hit", new WzIntProperty("attachfacing", 1))));

        var metadata = LifeLoader.BuildAttackInfoMetadata(infoNode, attackStateNode);

        Assert.NotNull(metadata);
        Assert.True(metadata.FrameHitAttachOverrides[1]);
        Assert.True(metadata.FrameFacingAttachOverrides[2]);
        Assert.True(metadata.ResolveHitAttach(2));
        Assert.True(metadata.ResolveFacingAttach(2));
    }

    [Fact]
    public void BuildAttackInfoMetadata_PrefersInfoHitFrameFlagsOverAttackFrameHitFlags()
    {
        WzSubProperty infoNode = CreateSubProperty(
            "info",
            CreateSubProperty(
                "hit",
                CreateSubProperty("1", new WzIntProperty("attach", 0))));
        WzSubProperty attackStateNode = CreateSubProperty(
            "attack1",
            CreateSubProperty("1", CreateSubProperty("hit", new WzIntProperty("attach", 1))),
            CreateSubProperty("2", CreateSubProperty("hit", new WzIntProperty("attach", 1))));

        var metadata = LifeLoader.BuildAttackInfoMetadata(infoNode, attackStateNode);

        Assert.NotNull(metadata);
        Assert.False(metadata.FrameHitAttachOverrides[1]);
        Assert.True(metadata.FrameHitAttachOverrides[2]);
    }

    [Fact]
    public void BuildAttackInfoMetadata_SuppressesAttackFrameFlagsWhenInfoAliasOwnsAttachState()
    {
        WzSubProperty infoNode = CreateSubProperty(
            "info",
            new WzIntProperty("attach", 0),
            new WzIntProperty("attachfacing", 0));
        WzSubProperty attackStateNode = CreateSubProperty(
            "attack1",
            CreateSubProperty("1", CreateSubProperty("hit", new WzIntProperty("attach", 1), new WzIntProperty("attachfacing", 1))));

        var metadata = LifeLoader.BuildAttackInfoMetadata(infoNode, attackStateNode);

        Assert.NotNull(metadata);
        Assert.Empty(metadata.FrameHitAttachOverrides);
        Assert.Empty(metadata.FrameFacingAttachOverrides);
        Assert.False(metadata.ResolveHitAttach(1));
        Assert.False(metadata.ResolveFacingAttach(1));
    }

    private static WzSubProperty CreateSubProperty(string name, params WzImageProperty[] children)
    {
        var property = new WzSubProperty(name);
        foreach (WzImageProperty child in children)
        {
            property.AddProperty(child);
        }

        return property;
    }
}
