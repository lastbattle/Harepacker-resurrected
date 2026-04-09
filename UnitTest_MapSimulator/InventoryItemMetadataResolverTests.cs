using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzProperties;
using Xunit;

namespace UnitTest_MapSimulator;

public sealed class InventoryItemMetadataResolverTests
{
    [Fact]
    public void BuildEffectLinesForTests_EmitsSignedFatigueLine()
    {
        WzSubProperty spec = new("spec");
        spec.AddProperty(new WzIntProperty("incFatigue", -30));

        IReadOnlyList<string> lines = InventoryItemMetadataResolver.BuildEffectLinesForTests(spec);

        Assert.Contains("Fatigue -30", lines);
    }

    [Fact]
    public void BuildMetadataLinesForTests_EmitsCashAvailabilityMetadata()
    {
        WzSubProperty info = new("info");
        info.AddProperty(new WzIntProperty("autoBuff", 1));
        info.AddProperty(new WzIntProperty("flatRate", 1));
        info.AddProperty(new WzIntProperty("limitMin", 30));

        IReadOnlyList<string> lines = InventoryItemMetadataResolver.BuildMetadataLinesForTests(info, specProperty: null);

        Assert.Contains("Auto Buff item", lines);
        Assert.Contains("Flat-rate item", lines);
        Assert.Contains("Time limit: 30 min", lines);
    }

    [Fact]
    public void BuildEffectLinesForTests_EmitsBuffItemPreviewLine()
    {
        WzSubProperty item = new("item");
        WzSubProperty buff = new("buff");
        WzSubProperty entry = new("0");
        entry.AddProperty(new WzIntProperty("buffItemID", 2028077));
        entry.AddProperty(new WzIntProperty("prob", 100));
        buff.AddProperty(entry);
        item.AddProperty(buff);

        IReadOnlyList<string> lines = InventoryItemMetadataResolver.BuildEffectLinesForTests(item, infoProperty: null, specProperty: null);

        Assert.Contains("Buff Item: Item #2028077", lines);
    }
}
