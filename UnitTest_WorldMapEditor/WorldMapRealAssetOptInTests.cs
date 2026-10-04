using HaCreator.WorldMap;
using HaCreator.MapSimulator.WorldMap;
using MapleLib.Img;
using MapleLib.WzLib;
using System;
using System.IO;

namespace UnitTest_WorldMapEditor;

/// <summary>
/// Opt-in checks for the supplied extracted clients. CI remains independent of
/// local WZ exports; set HACREATOR_WORLDMAP_LEGACY_ROOT or
/// HACREATOR_WORLDMAP_MODERN_ROOT to run the corresponding inventory checks.
/// </summary>
public sealed class WorldMapRealAssetOptInTests
{
    [Fact]
    public void ConfiguredLegacySource_MatchesDocumentedInventory()
    {
        string? root = ResolveRoot("HACREATOR_WORLDMAP_LEGACY_ROOT");
        if (root is null)
            return;

        using var source = new ImgFileSystemDataSource(root);
        IReadOnlyList<WzImage> images = LoadWorldMapImages(source);
        int surfaceCount = images.Count(image => !WorldMapExclusionList.IsExclusionImage(image.Name));
        Assert.Equal(26, surfaceCount);

        WzImage? helper = source.GetImage("Map", "MapHelper.img");
        if (helper is not null)
        {
            WorldMapMarkerRegistry registry = WorldMapMarkerRegistry.FromImage(helper);
            Assert.Equal(Enumerable.Range(0, 8), registry.Types);
        }
    }

    [Fact]
    public void ConfiguredModernSource_ResolvesLachelnAndFogFacts()
    {
        string? root = ResolveRoot("HACREATOR_WORLDMAP_MODERN_ROOT");
        if (root is null)
            return;

        using var source = new ImgFileSystemDataSource(root);
        IReadOnlyList<WzImage> images = LoadWorldMapImages(source);
        int surfaceCount = images.Count(image => !WorldMapExclusionList.IsExclusionImage(image.Name));
        Assert.Equal(104, surfaceCount);
        Assert.Equal(2, images.Count(image => WorldMapExclusionList.IsExclusionImage(image.Name)));

        WzImage? overviewImage = source.GetImage("Map", "WorldMap/WorldMap082.img");
        WzImage? detailImage = source.GetImage("Map", "WorldMap/WorldMap0823.img");
        if (overviewImage is not null && detailImage is not null)
        {
            WorldMapDocument overview = WorldMapCodec.Read(overviewImage);
            WorldMapDocument detail = WorldMapCodec.Read(detailImage);
            WorldMapMapEntry lacheln = Assert.Single(overview.Surface.Entries,
                entry => entry.Title == "Dream City Lacheln");
            Assert.Equal(29, lacheln.Type);
            Assert.Equal(24, lacheln.MapIds.Count);
            Assert.Contains(overview.Surface.Links, link => link.LinkMap == "WorldMap0823");
            Assert.Equal(24, detail.Surface.Entries.Count);
        }

        WzImage? fogImage = source.GetImage("Map", "WorldMap/WorldMap177.img");
        if (fogImage is not null)
        {
            WorldMapDocument fog = WorldMapCodec.Read(fogImage);
            Assert.Equal(4, fog.Surface.FogLayers.Count);
            Assert.All(fog.Surface.FogLayers, layer =>
            {
                Assert.True(layer.Quest.HasValue);
                Assert.True(layer.QState.HasValue);
                Assert.NotNull(layer.Image);
            });
        }
    }

    private static IReadOnlyList<WzImage> LoadWorldMapImages(IDataSource source)
    {
        var images = new List<WzImage>();
        foreach (string name in source.GetImageNamesInDirectory("Map", "WorldMap") ?? Array.Empty<string>())
        {
            string fileName = name.EndsWith(".img", StringComparison.OrdinalIgnoreCase) ? name : name + ".img";
            WzImage? image = source.GetImage("Map", "WorldMap/" + fileName)
                ?? source.GetImageByPath("Map/WorldMap/" + fileName);
            if (image is not null)
                images.Add(image);
        }
        return images;
    }

    private static string? ResolveRoot(string variable)
    {
        string value = Environment.GetEnvironmentVariable(variable);
        if (!string.IsNullOrWhiteSpace(value) && Directory.Exists(value))
            return value;
        return null;
    }
}
