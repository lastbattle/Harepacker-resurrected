using HaCreator.WorldMap;
using HaCreator.MapSimulator.WorldMap;
using MapleLib.WzLib.WzProperties;

namespace UnitTest_WorldMapEditor;

public sealed class WorldMapMarkerRegistryTests
{
    [Fact]
    public void FromProperty_DiscoversDynamicMarkerTypesAndUsageCounts()
    {
        var markerRoot = new WzSubProperty("mapImage");
        markerRoot.AddProperty(WorldMapFixtureFactory.CreateCanvas("0", 12, 14));
        WzCanvasProperty town = WorldMapFixtureFactory.CreateCanvas("29", 20, 22);
        town.AddProperty(new WzVectorProperty("origin", 10, 11));
        town.AddProperty(new WzIntProperty("z", 3));
        markerRoot.AddProperty(town);
        markerRoot.AddProperty(WorldMapFixtureFactory.CreateCanvas("1000", 24, 26));
        markerRoot.AddProperty(new WzStringProperty("metadata", "ignored"));

        WorldMapMarkerRegistry registry = WorldMapMarkerRegistry.FromProperty(markerRoot);

        Assert.Equal(new[] { 0, 29, 1000 }, registry.Types);
        Assert.True(registry.Contains(1000));
        Assert.True(registry.TryGet(29, out WorldMapMarkerAsset asset));
        Assert.Equal(20, asset.Width);
        Assert.Equal(22, asset.Height);
        Assert.Equal(3, asset.Z);

        WorldMapDocument document = WorldMapCodec.Read(WorldMapFixtureFactory.CreateSurface());
        WorldMapMarkerRegistry withUsage = registry.WithUsage(new[] { document });
        Assert.Equal(1, withUsage.Assets[29].UsageCount);
        Assert.Equal(0, withUsage.Assets[0].UsageCount);
    }
}
