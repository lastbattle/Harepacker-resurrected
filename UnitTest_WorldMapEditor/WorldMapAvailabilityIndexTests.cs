using HaCreator.WorldMap;
using HaCreator.MapSimulator.WorldMap;
using MapleLib.Img;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace UnitTest_WorldMapEditor;

public sealed class WorldMapAvailabilityIndexTests
{
    [Fact]
    public async Task GetAsync_IndexesNpcAndMobLifeWithoutDecodingEntityAssets()
    {
        const int mapId = WorldMapFixtureFactory.FirstMapId;
        var source = new InMemoryWorldMapDataSource();
        source.AddMap(mapId, LifeImage(
            ("0", "n", "9010000"),
            ("1", "m", "9300000"),
            ("2", "m", "9300000"),
            ("3", "m", "9400000"),
            ("4", "q", "not-an-entity")));
        // One asset is present and one is intentionally absent.  The index
        // should report this metadata while never loading the entity image.
        source.AddImage("Npc", "9010000.img", new WzImage("9010000.img"));
        source.AddImage("Mob", "9400000.img", new WzImage("9400000.img"));

        using var index = new WorldMapAvailabilityIndex(source, maxConcurrency: 1);
        WorldMapAvailabilityRecord record = await index.GetAsync(mapId);

        Assert.True(record.MapExists);
        Assert.Equal(1, record.NpcOccurrences["9010000"]);
        Assert.Equal(2, record.MobOccurrences["9300000"]);
        Assert.Equal(1, record.MobOccurrences["9400000"]);
        Assert.Empty(record.MissingNpcAssets);
        Assert.Contains("9300000", record.MissingMobAssets);
        Assert.Contains(record.Diagnostics, diagnostic => diagnostic.Contains("Unsupported life type 'q'"));
        Assert.Equal(1, index.CachedCount);

        // A cached request returns the same in-flight result and invalidation
        // forces a fresh metadata pass.
        WorldMapAvailabilityRecord cached = await index.GetAsync(mapId);
        Assert.Same(record, cached);
        index.Invalidate(mapId);
        Assert.Equal(0, index.CachedCount);
    }

    [Fact]
    public async Task GetAsync_DiagnosesCategorisedLifeAndKeepsFastPathData()
    {
        const int mapId = WorldMapFixtureFactory.SecondMapId;
        var source = new InMemoryWorldMapDataSource();
        WzImage image = LifeImage(("0", "n", "9001000"));
        ((WzSubProperty)image["life"]!).AddProperty(new WzIntProperty("isCategory", 1));
        source.AddMap(mapId, image);

        using var index = new WorldMapAvailabilityIndex(source);
        WorldMapAvailabilityRecord record = await index.GetAsync(mapId);

        Assert.True(record.HasCategorisedLife);
        Assert.Equal(1, record.NpcOccurrences["9001000"]);
        Assert.Contains(record.Diagnostics, diagnostic => diagnostic.Contains("Categorised life"));
    }

    [Fact]
    public async Task GetAsync_ReturnsDiagnosticRecordForUnknownMap()
    {
        using var index = new WorldMapAvailabilityIndex(new InMemoryWorldMapDataSource());

        WorldMapAvailabilityRecord record = await index.GetAsync(999999999);

        Assert.False(record.MapExists);
        Assert.Contains(record.Diagnostics, diagnostic => diagnostic.Contains("Map image not found"));
        Assert.Equal("Map/Map9/999999999.img", record.ImagePath);
    }

    private static WzImage LifeImage(params (string key, string type, string id)[] entries)
    {
        var image = new WzImage("life-fixture.img");
        var life = new WzSubProperty("life");
        foreach ((string key, string type, string id) in entries)
        {
            var entry = new WzSubProperty(key);
            entry.AddProperty(new WzStringProperty("type", type));
            entry.AddProperty(new WzStringProperty("id", id));
            life.AddProperty(entry);
        }
        image.AddProperty(life);
        image.Changed = false;
        image.Parsed = true;
        return image;
    }
}
