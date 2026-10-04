using System.Collections.Concurrent;
using System.Drawing;
using System.Threading;
using HaCreator.MapSimulator.Assets;
using HaCreator.MapSimulator.Contracts;
using MapleLib.Img;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace UnitTest_MapleGame;

public sealed class RuntimeAssetSourceTests
{
    [Fact]
    public void PropertyDescriptorOverridesResolveThroughCanonicalImageRoot()
    {
        using var authored = Image("house.img", 42);
        using var owner = new RuntimeOwnedAssetOverride(
            new RuntimeAssetKey("Map/Obj", "house.img/value"), authored, null);
        using var source = new FakeDataSource("empty");
        using var runtime = RuntimeAssetSource.CreateBorrowed(source, new[] { owner });

        WzImage root = runtime.FindImage("Map", "Obj/house.img");
        Assert.NotNull(root);
        Assert.Same(root, runtime.FindImage("Map/Obj", "house.img"));
        Assert.Same(root["value"], runtime.FindObject("Map/Obj", "house.img/value"));
        Assert.Equal(42, Assert.IsType<WzIntProperty>(root["value"]).Value);
        Assert.Same(root, Assert.Single(runtime.GetImagesInCategory("Map")));
    }

    [Fact]
    public void OwnedSourcePrefersDetachedOverridesAndDisposesItsSource()
    {
        var sourceImage = Image("100.img", 11);
        var overrideImage = Image("100.img", 22);
        var source = new FakeDataSource("fake", sourceImage);
        var key = new RuntimeAssetKey("Map", "100.img");
        using var owner = new RuntimeOwnedAssetOverride(key, overrideImage, null);

        using (RuntimeAssetSource runtime = RuntimeAssetSource.CreateOwned(source, new[] { owner }))
        {
            Assert.Equal(RuntimeAssetSourceOwnership.Owned, runtime.Ownership);
            Assert.Equal("v1", runtime.Version.Version);

            WzImage resolved = runtime.FindImage("map", "100.img");
            Assert.NotNull(resolved);
            Assert.NotSame(overrideImage, resolved);
            Assert.Equal(22, Assert.IsType<WzIntProperty>(resolved["value"]).Value);

            // A caller receives a detached root, so mutating it cannot change the
            // source image or the root retained by the override store.
            Assert.IsType<WzIntProperty>(resolved["value"]).Value = 99;
            WzImage secondRead = runtime.FindImage("Map", "100.img");
            Assert.Same(resolved, secondRead);
            Assert.Equal(99, Assert.IsType<WzIntProperty>(secondRead["value"]).Value);
            Assert.Equal(11, Assert.IsType<WzIntProperty>(sourceImage["value"]).Value);
        }

        Assert.Equal(1, source.DisposeCount);
        sourceImage.Dispose();
        overrideImage.Dispose();
    }

    [Fact]
    public void BorrowedSourceIsNotDisposedAndConcurrentReadsAreSerialized()
    {
        var sourceImage = Image("asset.img", 7);
        var source = new FakeDataSource("borrowed", sourceImage) { DelayMilliseconds = 10 };
        using (RuntimeAssetSource runtime = new(source, RuntimeAssetSourceOwnership.Borrowed))
        {
            Parallel.For(0, 20, _ =>
            {
                WzImage image = runtime.FindImage("Map", "asset.img");
                Assert.NotNull(image);
            });

            Assert.Equal(1, source.MaximumConcurrentReads);
        }

        Assert.Equal(0, source.DisposeCount);
        sourceImage.Dispose();
        source.Dispose();
    }

    [Fact]
    public void EnumerationsMergeOverridesAndReplaceMatchingSourceImages()
    {
        var sourceImage = Image("same.img", 1);
        var sourceOnlyImage = Image("source.img", 2);
        var overrideImage = Image("same.img", 3);
        var extraImage = Image("extra.img", 4);
        var secondExtraImage = Image("extra.img", 5);
        var source = new FakeDataSource("fake", sourceImage, sourceOnlyImage);
        using var sameOwner = new RuntimeOwnedAssetOverride(
            new RuntimeAssetKey("Map", "same.img"), overrideImage, null);
        using var extraOwner = new RuntimeOwnedAssetOverride(
            new RuntimeAssetKey("Map", "nested/extra.img"), extraImage, null);
        using var secondExtraOwner = new RuntimeOwnedAssetOverride(
            new RuntimeAssetKey("Map", "deeper/extra.img"), secondExtraImage, null);

        using (RuntimeAssetSource runtime = RuntimeAssetSource.CreateBorrowed(
                   source, new[] { sameOwner, extraOwner, secondExtraOwner }))
        {
            IReadOnlyList<WzImage> images = runtime.GetImagesInCategory("Map");
            Assert.Equal(4, images.Count);
            Assert.Equal(2, images.Count(image => image.Name == "extra.img"));
            Assert.Contains(images, image =>
                Assert.IsType<WzIntProperty>(image["value"]).Value == 4);
            Assert.Contains(images, image =>
                Assert.IsType<WzIntProperty>(image["value"]).Value == 5);
            Assert.Same(images.Single(image => image.Name == "same.img"),
                runtime.FindImage("Map", "same.img"));
            Assert.Same(images.Single(image =>
                    Assert.IsType<WzIntProperty>(image["value"]).Value == 4),
                runtime.FindImage("Map", "nested/extra.img"));

            Assert.Equal(new[] { "extra" }, runtime.GetImageNamesInDirectory("Map", "nested"));
            Assert.Contains("nested", runtime.GetSubdirectories("Map"));
            Assert.Contains("deeper", runtime.GetSubdirectories("Map"));
        }

        sourceImage.Dispose();
        sourceOnlyImage.Dispose();
        overrideImage.Dispose();
        extraImage.Dispose();
        secondExtraImage.Dispose();
        source.Dispose();
    }

    [Fact]
    public void DisposeReleasesSessionRootsEvenWhenOwnedSourceDisposalFails()
    {
        var sourceImage = Image("base.img", 1);
        var overrideImage = Image("override.img", 2);
        var source = new FakeDataSource("fake", sourceImage) { ThrowOnDispose = true };
        using var owner = new RuntimeOwnedAssetOverride(
            new RuntimeAssetKey("Map", "override.img"), overrideImage, null);
        var runtime = RuntimeAssetSource.CreateOwned(source, new[] { owner });
        WzImage sourceRoot = runtime.FindImage("Map", "base.img");
        WzImage overrideRoot = runtime.FindImage("Map", "override.img");

        Assert.Throws<InvalidOperationException>(() => runtime.Dispose());
        Assert.Null(sourceRoot.Name);
        Assert.Null(overrideRoot.Name);
        Assert.Equal(1, source.DisposeCount);
        runtime.Dispose();

        sourceImage.Dispose();
        overrideImage.Dispose();
    }

    [Fact]
    public void ConstructorFailureDisposesOwnedSourceAndOverrideStore()
    {
        var source = new FakeDataSource("fake") { ThrowOnName = true };
        var image = Image("override.img", 3);
        var store = new RuntimeAssetOverrideStore();
        store.Set(new RuntimeAssetKey("Map", "override.img"), image);

        Assert.Throws<InvalidOperationException>(() =>
            new RuntimeAssetSource(source, RuntimeAssetSourceOwnership.Owned, store));
        Assert.Equal(1, source.DisposeCount);
        Assert.Throws<ObjectDisposedException>(() => _ = store.Count);
        image.Dispose();
    }

    [Fact]
    public void DetachedInlinkSurvivesSourceDisposalWithoutMutatingSource()
    {
        WzImage sourceImage = ImageWithProperties("linked.img",
            Canvas("target", Color.CornflowerBlue),
            Canvas("alias", Color.Transparent,
                (WzCanvasProperty.InlinkPropertyName, "target")));
        var source = new FakeDataSource("fake", sourceImage);

        using (RuntimeAssetSource runtime = RuntimeAssetSource.CreateBorrowed(source))
        {
            WzImage detached = runtime.FindImage("Map", "linked.img");
            WzCanvasProperty alias = Assert.IsType<WzCanvasProperty>(detached["alias"]);

            Assert.False(alias.ContainsInlinkProperty());
            Assert.Equal(1, alias.PngProperty.Width);
            Assert.Equal(1, alias.PngProperty.Height);
            Assert.True(Assert.IsType<WzCanvasProperty>(sourceImage["alias"])
                .ContainsInlinkProperty());

            source.Dispose();
            Assert.NotNull(alias.PngProperty.GetCompressedBytesForExtraction(false));
        }

        sourceImage.Dispose();
    }

    [Fact]
    public void DetachedOutlinkUsesSessionTargetCloneAndSurvivesSourceDisposal()
    {
        WzImage targetImage = ImageWithProperties("target.img",
            Canvas("frame", Color.MediumSeaGreen));
        WzImage sourceImage = ImageWithProperties("source.img",
            Canvas("alias", Color.Transparent,
                (WzCanvasProperty.OutlinkPropertyName, "Map/target.img/frame")));
        var source = new FakeDataSource("fake", sourceImage, targetImage);

        using (RuntimeAssetSource runtime = RuntimeAssetSource.CreateBorrowed(source))
        {
            WzImage detached = runtime.FindImage("Map", "source.img");
            WzCanvasProperty alias = Assert.IsType<WzCanvasProperty>(detached["alias"]);

            Assert.False(alias.ContainsOutlinkProperty());
            Assert.NotNull(alias.PngProperty.GetCompressedBytesForExtraction(false));
            Assert.True(Assert.IsType<WzCanvasProperty>(sourceImage["alias"])
                .ContainsOutlinkProperty());

            source.Dispose();
            Assert.Equal(1, alias.PngProperty.Width);
        }

        sourceImage.Dispose();
        targetImage.Dispose();
    }

    [Fact]
    public void ExternalUolIsMaterializedFromSessionOwnedTarget()
    {
        WzImage targetImage = Image("target.img", 77);
        WzImage sourceImage = ImageWithProperties("source.img",
            new WzUOLProperty("alias", "Map/target.img/value"));
        var source = new FakeDataSource("fake", sourceImage, targetImage);

        using (RuntimeAssetSource runtime = RuntimeAssetSource.CreateBorrowed(source))
        {
            WzImage detached = runtime.FindImage("Map", "source.img");
            Assert.Equal(77, Assert.IsType<WzIntProperty>(detached["alias"]).Value);
            Assert.IsNotType<WzUOLProperty>(detached["alias"]);
        }

        sourceImage.Dispose();
        targetImage.Dispose();
    }

    [Fact]
    public void CyclicOutlinksResolveOnceWithoutRecursiveOverflow()
    {
        WzImage first = ImageWithProperties("first.img",
            Canvas("frame", Color.Transparent,
                (WzCanvasProperty.OutlinkPropertyName, "Map/second.img/frame")));
        WzImage second = ImageWithProperties("second.img",
            Canvas("frame", Color.Gold,
                (WzCanvasProperty.OutlinkPropertyName, "Map/first.img/frame")));
        var source = new FakeDataSource("fake", first, second);

        using (RuntimeAssetSource runtime = RuntimeAssetSource.CreateBorrowed(source))
        {
            WzCanvasProperty firstFrame = Assert.IsType<WzCanvasProperty>(
                runtime.FindImage("Map", "first.img")["frame"]);
            WzCanvasProperty secondFrame = Assert.IsType<WzCanvasProperty>(
                runtime.FindImage("Map", "second.img")["frame"]);

            Assert.False(firstFrame.ContainsOutlinkProperty());
            Assert.False(secondFrame.ContainsOutlinkProperty());
            Assert.NotNull(firstFrame.PngProperty.GetCompressedBytesForExtraction(false));
            Assert.NotNull(secondFrame.PngProperty.GetCompressedBytesForExtraction(false));
        }

        first.Dispose();
        second.Dispose();
    }

    private static WzImage Image(string name, int value)
    {
        var image = new WzImage(name) { Parsed = true };
        image.AddProperty(new WzIntProperty("value", value));
        return image;
    }

    private static WzImage ImageWithProperties(string name, params WzImageProperty[] properties)
    {
        var image = new WzImage(name) { Parsed = true };
        foreach (WzImageProperty property in properties)
            image.AddProperty(property);
        return image;
    }

    private static WzCanvasProperty Canvas(
        string name,
        Color color,
        params (string Name, string Value)[] links)
    {
        var canvas = new WzCanvasProperty(name)
        {
            PngProperty = Png(color)
        };
        foreach ((string linkName, string linkValue) in links)
            canvas.AddProperty(new WzStringProperty(linkName, linkValue));
        return canvas;
    }

    private static WzPngProperty Png(Color color)
    {
        using var bitmap = new Bitmap(1, 1);
        bitmap.SetPixel(0, 0, color);
        var png = new WzPngProperty();
        png.PNG = bitmap;
        return png;
    }

    private sealed class FakeDataSource : IDataSource
    {
        private readonly Dictionary<string, WzImage> _images =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly string _name;
        private int _activeReads;
        private int _maximumConcurrentReads;

        public FakeDataSource(string name, params WzImage[] images)
        {
            _name = name;
            foreach (WzImage image in images)
                _images[Key("Map", image.Name)] = image;
            VersionInfo = new VersionInfo { Version = "v1", DisplayName = "Fake v1" };
        }

        public bool ThrowOnName { get; set; }
        public bool ThrowOnDispose { get; set; }
        public string Name => ThrowOnName ? throw new InvalidOperationException("name failure") : _name;
        public bool IsInitialized => true;
        public VersionInfo VersionInfo { get; }
        public int DelayMilliseconds { get; set; }
        public int MaximumConcurrentReads => _maximumConcurrentReads;
        public int DisposeCount { get; private set; }

        public WzImage GetImage(string category, string imageName)
        {
            int active = Interlocked.Increment(ref _activeReads);
            UpdateMaximum(active);
            try
            {
                if (DelayMilliseconds > 0)
                    Thread.Sleep(DelayMilliseconds);
                _images.TryGetValue(Key(category, imageName), out WzImage image);
                return image;
            }
            finally
            {
                Interlocked.Decrement(ref _activeReads);
            }
        }

        public WzImage GetImageByPath(string relativePath)
        {
            string[] parts = (relativePath ?? string.Empty).Split('/', 2);
            return parts.Length == 2 ? GetImage(parts[0], parts[1]) : null;
        }

        public IEnumerable<WzImage> GetImagesInCategory(string category)
        {
            string prefix = Key(category, string.Empty);
            return _images.Where(pair => pair.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(pair => pair.Value).ToArray();
        }

        public IEnumerable<WzImage> GetImagesInDirectory(string category, string subDirectory) =>
            GetImagesInCategory(category).Where(image =>
                image.Name.StartsWith((subDirectory ?? string.Empty) + "/", StringComparison.OrdinalIgnoreCase));

        public IEnumerable<string> GetImageNamesInDirectory(string category, string subDirectory) =>
            GetImagesInDirectory(category, subDirectory).Select(image => image.Name);

        public bool ImageExists(string category, string imageName) => GetImage(category, imageName) != null;
        public bool CategoryExists(string category) => GetCategories().Contains(category, StringComparer.OrdinalIgnoreCase);
        public IEnumerable<string> GetCategories() => new[] { "Map" };
        public IEnumerable<string> GetSubdirectories(string category) => Array.Empty<string>();
        public WzDirectory GetDirectory(string category) => null;
        public IEnumerable<WzDirectory> GetDirectories(string baseCategory) => Array.Empty<WzDirectory>();
        public void PreloadCategory(string category) { }
        public void ClearCache() { }
        public DataSourceStats GetStats() => new();
        public bool SaveImage(string category, WzImage image, string relativePath = null) =>
            throw new NotSupportedException();
        public void MarkImageUpdated(string category, WzImage image) =>
            throw new NotSupportedException();

        public void Dispose()
        {
            DisposeCount++;
            if (ThrowOnDispose)
                throw new InvalidOperationException("dispose failure");
        }

        private static string Key(string category, string imageName) =>
            (category ?? string.Empty).Trim('/') + "/" + (imageName ?? string.Empty).Trim('/');

        private void UpdateMaximum(int active)
        {
            while (true)
            {
                int observed = _maximumConcurrentReads;
                if (active <= observed || Interlocked.CompareExchange(
                        ref _maximumConcurrentReads, active, observed) == observed)
                    return;
            }
        }
    }
}
