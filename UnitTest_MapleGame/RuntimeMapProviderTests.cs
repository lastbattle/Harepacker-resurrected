using HaCreator.MapSimulator.Assets;
using HaCreator.MapSimulator.Contracts;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System.IO;

namespace UnitTest_MapleGame;

public sealed class RuntimeMapProviderTests
{
    [Fact]
    public void SnapshotWinsAndEveryLoadOwnsIndependentData()
    {
        using WzImage saved = MapImage("100000000.img", "saved");
        using WzImage authored = MapImage("100000000.img", "unsaved");
        var source = new Source(saved);
        var services = new RuntimeDataServices(source, new SourceRuntimeAssetCatalog(source));
        var snapshot = new WzRuntimeMapReader().Read(authored,
            new WzRuntimeMapReaderOptions { MapId = 100000000, MapName = "Unsaved map" });
        using var provider = new RuntimeMapProvider(services, new[] { snapshot });
        snapshot.Dispose();
        authored.Dispose();

        using (RuntimeMapDefinition first = provider.Load(100000000))
        {
            var info = first.CreateMapInfo();
            try
            {
                Assert.Equal("unsaved", info.bgm);
                info.bgm = "changed by caller";
                Assert.Equal("Unsaved map", first.MapName);
            }
            finally { info.Image.Dispose(); }
        }
        using RuntimeMapDefinition second = provider.Load(100000000);
        var secondInfo = second.CreateMapInfo();
        try { Assert.Equal("unsaved", secondInfo.bgm); }
        finally { secondInfo.Image.Dispose(); }
        Assert.Empty(source.RequestedPaths);
    }

    [Fact]
    public void SavedMapUsesCanonicalPathAndCancellationPreventsSourceAccess()
    {
        using WzImage image = MapImage("100000000.img", "saved");
        var source = new Source(image);
        var services = new RuntimeDataServices(source, new SourceRuntimeAssetCatalog(source));
        using var provider = new RuntimeMapProvider(services);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => provider.Load(100000000, cancellation.Token));
        Assert.Empty(source.RequestedPaths);
        using RuntimeMapDefinition map = provider.Load(100000000);
        Assert.Equal(100000000, map.MapId);
        Assert.Contains("Map/Map/Map1/100000000.img", source.RequestedPaths);
        Assert.Throws<ArgumentOutOfRangeException>(() => provider.Load(-1));
        Assert.Throws<FileNotFoundException>(() => provider.Load(200000000));
        provider.Dispose();
        Assert.Throws<ObjectDisposedException>(() => provider.Load(100000000));
    }

    private static WzImage MapImage(string name, string bgm)
    {
        var image = new WzImage(name) { Parsed = true };
        var info = new WzSubProperty("info");
        info.AddProperty(new WzStringProperty("bgm", bgm));
        info.AddProperty(new WzIntProperty("VRLeft", -100));
        info.AddProperty(new WzIntProperty("VRRight", 100));
        info.AddProperty(new WzIntProperty("VRTop", -50));
        info.AddProperty(new WzIntProperty("VRBottom", 50));
        image.AddProperty(info);
        return image;
    }

    private sealed class Source(WzImage image) : IRuntimeAssetSource
    {
        public List<string> RequestedPaths { get; } = new();
        public string Name => "fixture";
        public bool IsInitialized => true;
        public RuntimeAssetSourceOwnership Ownership => RuntimeAssetSourceOwnership.Borrowed;
        public RuntimeAssetSourceVersion Version => null;
        public WzImage FindImage(string category, string path)
        {
            RequestedPaths.Add(category + "/" + path);
            return category == "Map" && path == "Map/Map1/" + image.Name ? image : null;
        }
        public WzObject FindObject(string category, string path) => null;
        public IReadOnlyList<WzImage> GetImagesInCategory(string category) => Array.Empty<WzImage>();
        public IReadOnlyList<WzImage> GetImagesInDirectory(string category, string subDirectory) => Array.Empty<WzImage>();
        public IReadOnlyList<string> GetImageNamesInDirectory(string category, string subDirectory) => Array.Empty<string>();
        public bool ImageExists(string category, string imageName) => false;
        public bool CategoryExists(string category) => true;
        public IReadOnlyList<string> GetCategories() => new[] { "Map" };
        public IReadOnlyList<string> GetSubdirectories(string category) => Array.Empty<string>();
        public IReadOnlyList<WzDirectory> GetDirectories(string baseCategory) => Array.Empty<WzDirectory>();
        public void PreloadCategory(string category) { }
    }
}
