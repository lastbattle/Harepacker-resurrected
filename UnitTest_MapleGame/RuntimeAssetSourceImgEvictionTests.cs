using System.IO;
using HaCreator.MapSimulator.Assets;
using MapleLib.Img;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace UnitTest_MapleGame;

public sealed class RuntimeAssetSourceImgEvictionTests
{
    [Fact]
    public void DetachedImageSurvivesImgCacheEvictionAndSourceDisposal()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"MapleGameRuntimeAssets_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "Map"));

        var config = new HaCreatorConfig
        {
            ImgRootPath = root,
            Cache = new CacheConfig { MaxMemoryCacheMB = 1 }
        };

        try
        {
            WriteImage(root, config, "retained.img", "retained");
            WriteImage(root, config, "evictor.img", "evictor");

            ImgFileSystemDataSource source = new(root, config);
            RuntimeAssetSource runtime = RuntimeAssetSource.CreateBorrowed(source);
            try
            {
                WzImage sourceRetained = source.GetImage("Map", "retained.img");
                Assert.NotNull(sourceRetained);

                WzImage detached = runtime.FindImage("Map", "retained.img");
                Assert.NotNull(detached);
                Assert.NotSame(sourceRetained, detached);
                Assert.Equal(
                    "survives-source-disposal",
                    Assert.IsType<WzStringProperty>(detached["marker"]).Value);

                // Each image is below the one-megabyte source cache limit, but
                // the pair is above it by the manager's parsed-property size
                // estimate. Loading the second image therefore evicts the
                // source root used above.
                Assert.NotNull(source.GetImage("Map", "evictor.img"));
                DataSourceStats stats = source.GetStats();
                Assert.Equal(1, stats.CachedImageCount);

                WzImage reloadedRetained = source.GetImage("Map", "retained.img");
                Assert.NotNull(reloadedRetained);
                Assert.NotSame(sourceRetained, reloadedRetained);

                source.Dispose();
                Assert.Equal(
                    "survives-source-disposal",
                    Assert.IsType<WzStringProperty>(detached["marker"]).Value);
            }
            finally
            {
                runtime.Dispose();
                source.Dispose();
            }
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static void WriteImage(
        string root,
        HaCreatorConfig config,
        string name,
        string payloadMarker)
    {
        using var manager = new ImgFileSystemManager(root, config, WzMapleVersion.GMS);
        using var image = new WzImage(name);
        image.AddProperty(new WzStringProperty("marker", "survives-source-disposal"));
        image.AddProperty(new WzStringProperty(
            "payload",
            new string(payloadMarker[0], 300_000)));

        Assert.True(manager.SaveImageToFile(
            image,
            Path.Combine(root, "Map", name)));
    }
}
