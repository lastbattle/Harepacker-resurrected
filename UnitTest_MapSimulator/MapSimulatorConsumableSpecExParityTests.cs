using HaCreator.MapSimulator;
using MapleLib.WzLib.WzProperties;

namespace UnitTest_MapSimulator;

public sealed class MapSimulatorConsumableSpecExParityTests
{
    [Fact]
    public void ResolveConsumableIntValueForTests_FallsBackToSpecExWhenSpecIsMissing()
    {
        var spec = new WzSubProperty("spec");
        var specEx = new WzSubProperty("specEx");
        specEx.AddProperty(new WzIntProperty("moveTo", 100000000));

        int resolved = MapSimulator.ResolveConsumableIntValueForTests(spec, specEx, "moveTo");

        Assert.Equal(100000000, resolved);
    }

    [Fact]
    public void ResolveConsumableIntValueForTests_PrefersSpecOverSpecEx()
    {
        var spec = new WzSubProperty("spec");
        spec.AddProperty(new WzIntProperty("moveTo", 101000000));
        var specEx = new WzSubProperty("specEx");
        specEx.AddProperty(new WzIntProperty("moveTo", 100000000));

        int resolved = MapSimulator.ResolveConsumableIntValueForTests(spec, specEx, "moveTo");

        Assert.Equal(101000000, resolved);
    }

    [Fact]
    public void ResolveConsumablePercentValueForTests_UsesSpecExWhenSpecHasNoPositiveValue()
    {
        var spec = new WzSubProperty("spec");
        var specEx = new WzSubProperty("specEx");
        specEx.AddProperty(new WzIntProperty("plusExpRate", 20));

        int resolved = MapSimulator.ResolveConsumablePercentValueForTests(spec, specEx, "expBuff", "expR", "plusExpRate");

        Assert.Equal(20, resolved);
    }

    [Fact]
    public void ResolveConsumableEnvironmentalDamageProtectionForTests_UsesFallbackWhenPrimaryIsZero()
    {
        var thaw = new WzIntProperty("thaw", 0);
        var thawEx = new WzIntProperty("thaw", -6);

        int resolved = MapSimulator.ResolveConsumableEnvironmentalDamageProtectionForTests(thaw, thawEx);

        Assert.Equal(6, resolved);
    }

    [Fact]
    public void ResolveConsumableRandomMorphTemplateIdsForTests_MergesSpecAndSpecExPools()
    {
        var primary = new WzSubProperty("morphRandom");
        primary.AddProperty(CreateMorphEntry("0", morphTemplateId: 1500, prop: 2));

        var fallback = new WzSubProperty("morphRandom");
        fallback.AddProperty(CreateMorphEntry("0", morphTemplateId: 1600, prop: 1));

        int[] merged = MapSimulator.ResolveConsumableRandomMorphTemplateIdsForTests(primary, fallback);

        Assert.Equal(new[] { 1500, 1500, 1600 }, merged);
    }

    private static WzSubProperty CreateMorphEntry(string name, int morphTemplateId, int prop)
    {
        var entry = new WzSubProperty(name);
        entry.AddProperty(new WzIntProperty("morph", morphTemplateId));
        entry.AddProperty(new WzIntProperty("prop", prop));
        return entry;
    }
}
