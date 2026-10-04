using HaCreator.MapSimulator.Assets;
using HaCreator.MapSimulator.Contracts;
using MapleLib.Img;
using MapleLib.WzLib;
using System.IO;

namespace UnitTest_MapleGame;

public sealed class RuntimeRealMapTests
{
    [LocalMapTheory]
    [InlineData("gms_v95")]
    [InlineData("gms_v270")]
    public void ItemCatalogTraversesScalarStringLeaves(string version)
    {
        string root = Path.Combine(Environment.GetEnvironmentVariable("MAPLEGAME_TEST_EXPORTS")!, version);
        using var source = RuntimeAssetSourceFactory.OpenImgDirectory(root);
        var catalog = new SourceRuntimeAssetCatalog(source);
        Assert.True(catalog.TryGetItemName(2000000, out var item));
        Assert.False(string.IsNullOrWhiteSpace(item.Name));
        Assert.NotEmpty(catalog.GetItemNames());
    }

    [LocalMapTheory]
    [InlineData("gms_v95", 100000000)]
    [InlineData("gms_v270", 100000000)]
    public void OwnedImgSessionLoadsAfterEditorSourceIsDisposed(string version, int mapId)
    {
        string root = Path.Combine(Environment.GetEnvironmentVariable("MAPLEGAME_TEST_EXPORTS")!, version);
        using var session = RuntimeAssetSourceFactory.OpenImgDirectory(root);
        string path = $"Map/Map{mapId.ToString("D9")[0]}/{mapId:D9}.img";
        using (var editor = new ImgFileSystemDataSource(root))
        {
            Assert.NotNull(editor.GetImage("Map", path));
            editor.ClearCache();
        }

        Assert.Equal(RuntimeAssetSourceOwnership.Owned, session.Ownership);
        WzImage image = session.FindImage("Map", path);
        Assert.NotNull(image);
        Assert.NotEmpty(image.WzProperties);
        using var definition = new WzRuntimeMapReader().Read(image,
            new WzRuntimeMapReaderOptions { MapId = mapId, AssetSource = session });
        Assert.Equal(mapId, definition.MapId);
        Assert.NotEmpty(definition.Footholds);
    }

    [LocalMapTheory]
    [InlineData("gms_v95", 100000000)]
    [InlineData("gms_v270", 100000000)]
    [InlineData("gms_v270", 450003000)]
    public void RealMapDescriptorsSurviveSourceDisposal(string version, int mapId)
    {
        string root = Path.Combine(Environment.GetEnvironmentVariable("MAPLEGAME_TEST_EXPORTS")!, version);
        RuntimeMapDefinition definition;
        int portalCount;
        using (var source = new ImgFileSystemDataSource(root))
        {
            string name = mapId.ToString("D9");
            WzImage map = source.GetImage("Map", $"Map/Map{name[0]}/{name}.img");
            Assert.NotNull(map);
            if (!map.Parsed) Assert.True(map.ParseImage());
            portalCount = map["portal"].WzProperties.Count;
            definition = new WzRuntimeMapReader().Read(map,
                new WzRuntimeMapReaderOptions { MapId = mapId });
        }

        using (definition)
        {
            Assert.Equal(mapId, definition.MapId);
            Assert.Equal(portalCount, definition.Portals.Count);
            Assert.NotEmpty(definition.Footholds);
            Assert.True(definition.MapSize.X > 0 && definition.MapSize.Y > 0);
            var firstCopy = definition.CreateMapInfo();
            var secondCopy = definition.CreateMapInfo();
            try
            {
                Assert.NotSame(firstCopy.Image, secondCopy.Image);
                Assert.Equal(firstCopy.bgm, secondCopy.bgm);
                Assert.NotNull(secondCopy.Image["info"]);
            }
            finally
            {
                firstCopy.Image.Dispose();
                secondCopy.Image.Dispose();
            }
        }
    }

    public sealed class LocalMapTheoryAttribute : TheoryAttribute
    {
        public LocalMapTheoryAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MAPLEGAME_TEST_EXPORTS")))
                Skip = "Set MAPLEGAME_TEST_EXPORTS to a directory containing gms_v95 and gms_v270 exports.";
        }
    }
}
