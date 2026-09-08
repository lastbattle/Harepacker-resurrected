using System;
using System.Collections.Generic;
using HaCreator.MapSimulator.Assets;
using HaCreator.MapSimulator.WorldMap;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace UnitTest_MapleGame;

public sealed class WorldMapSourceReaderTests
{
    [Fact]
    public void ReadDocumentsUsesInjectedSourceAndReportsMissingImages()
    {
        using WzImage surface = CreateSurface("WorldMap/Alpha.img", "Alpha");
        var source = new FixtureAssetSource(
            new[] { "WorldMap/Alpha.img", "WorldMap/SearchExcept.img", "WorldMap/Missing.img" },
            new Dictionary<string, WzImage>(StringComparer.OrdinalIgnoreCase)
            {
                ["Map/WorldMap/Alpha.img"] = surface
            });

        WorldMapReadResult result = new WorldMapSourceReader(source).ReadDocuments();

        WorldMapDocument document = Assert.Single(result.Documents);
        Assert.Equal("Alpha", document.Surface.LogicalName);
        WorldMapReadDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("Missing", diagnostic.ImageName);
        Assert.Contains("could not be loaded", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static WzImage CreateSurface(string imageName, string logicalName)
    {
        var image = new WzImage(imageName) { Parsed = true };
        var info = new WzSubProperty("info");
        info.AddProperty(new WzStringProperty("WorldMap", logicalName));
        image.AddProperty(info);
        return image;
    }

    private sealed class FixtureAssetSource : IRuntimeAssetSource
    {
        private readonly IReadOnlyList<string> _names;
        private readonly IReadOnlyDictionary<string, WzImage> _images;

        public FixtureAssetSource(
            IReadOnlyList<string> names,
            IReadOnlyDictionary<string, WzImage> images)
        {
            _names = names;
            _images = images;
        }

        public string Name => "fixture";
        public bool IsInitialized => true;
        public RuntimeAssetSourceOwnership Ownership => RuntimeAssetSourceOwnership.Borrowed;
        public RuntimeAssetSourceVersion Version => null;

        public WzImage FindImage(string category, string imageName) =>
            _images.TryGetValue(Key(category, imageName), out WzImage image) ? image : null;

        public WzObject FindObject(string category, string name) => null;
        public IReadOnlyList<WzImage> GetImagesInCategory(string category) => Array.Empty<WzImage>();
        public IReadOnlyList<WzImage> GetImagesInDirectory(string category, string subDirectory) =>
            Array.Empty<WzImage>();
        public IReadOnlyList<string> GetImageNamesInDirectory(string category, string subDirectory) => _names;
        public bool ImageExists(string category, string imageName) => FindImage(category, imageName) != null;
        public bool CategoryExists(string category) => true;
        public IReadOnlyList<string> GetCategories() => new[] { "Map" };
        public IReadOnlyList<string> GetSubdirectories(string category) => new[] { "WorldMap" };
        public IReadOnlyList<WzDirectory> GetDirectories(string baseCategory) => Array.Empty<WzDirectory>();
        public void PreloadCategory(string category) { }

        private static string Key(string category, string imageName) =>
            $"{(category ?? string.Empty).Trim('/')}/{(imageName ?? string.Empty).Trim('/')}";
    }
}
