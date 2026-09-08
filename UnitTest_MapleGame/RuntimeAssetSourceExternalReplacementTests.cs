using System.Drawing;
using System.IO;
using HaCreator.MapSimulator.Assets;
using HaCreator.MapSimulator.Contracts;
using MapleLib.Img;
using MapleLib.WzLib;
using MapleLib.WzLib.Serializer;
using MapleLib.WzLib.WzProperties;

namespace UnitTest_MapleGame;

public sealed class RuntimeAssetSourceExternalReplacementTests
{
    [Fact]
    public void ExternalReplacementPreservesDetachedDataAndCorruptDestinationFailsThroughProvider()
    {
        string temporaryRoot = Path.GetFullPath(Path.GetTempPath());
        string fixture = Path.Combine(temporaryRoot, "MapleGame-external-replacement-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(fixture, "Map", "Obj"));
        Directory.CreateDirectory(Path.Combine(fixture, "Map", "Map", "Map0"));
        try
        {
            string assetPath = Path.Combine(fixture, "Map", "Obj", "replacement.img");
            string destinationPath = Path.Combine(fixture, "Map", "Map", "Map0", "000020000.img");
            WriteAsset(assetPath, 1, Color.Magenta);
            WriteMap(destinationPath);

            var configuration = new HaCreatorConfig();
            configuration.HotSwap.Enabled = false;
            using var backingSource = new ImgFileSystemDataSource(fixture, configuration);
            using RuntimeAssetSource active = RuntimeAssetSource.CreateBorrowed(backingSource);
            WzImage detached = active.FindImage("Map/Obj", "replacement.img");
            Assert.NotNull(detached);
            // Do not decode its canvas/raw payload yet. The facade must have
            // detached the lazy bytes before any backing-file replacement.
            Assert.Equal(1, ((WzIntProperty)detached["marker"]).Value);

            string stagedAsset = Path.Combine(fixture, "replacement-next.img");
            WriteAsset(stagedAsset, 2, Color.Lime);
            try
            {
                File.Move(stagedAsset, assetPath, overwrite: true);
            }
            catch (Exception replacementError) when (replacementError is UnauthorizedAccessException or IOException)
            {
                // Windows can deny replacement while the source's lazy reader
                // is open. Verify that denial leaves the original file intact.
                Assert.True(File.Exists(stagedAsset));
                Assert.True(File.Exists(assetPath));
                Assert.Equal(1, ((WzIntProperty)detached["marker"]).Value);
            }
            // Close the borrowed source (including its cached readers), while
            // the runtime facade continues owning its already detached image.
            // ClearCache only drops LRU references and does not close readers.
            backingSource.Dispose();
            if (File.Exists(stagedAsset)) File.Move(stagedAsset, assetPath, overwrite: true);
            using RuntimeAssetSource failureSource = RuntimeAssetSourceFactory.OpenImgDirectory(fixture);
            string stagedCorruption = Path.Combine(fixture, "corrupt-next.img");
            File.WriteAllBytes(stagedCorruption, new byte[] { 0xFF, 0x00, 0x01 });
            File.Move(stagedCorruption, destinationPath, overwrite: true);

            Assert.Same(detached, active.FindImage("Map/Obj", "replacement.img"));
            AssertAsset(detached, 1, Color.Magenta);

            var services = new RuntimeDataServices(failureSource, new SourceRuntimeAssetCatalog(failureSource));
            using var provider = new RuntimeMapProvider(services);
            FileNotFoundException error = Assert.Throws<FileNotFoundException>(() => provider.Load(20000));
            Assert.Contains("000020000", error.Message);
            // A failed uncached read cannot dispose or invalidate published roots.
            AssertAsset(detached, 1, Color.Magenta);

            string stagedRepair = Path.Combine(fixture, "repaired-next.img");
            WriteMap(stagedRepair);
            File.Move(stagedRepair, destinationPath, overwrite: true);
            using RuntimeAssetSource fresh = RuntimeAssetSourceFactory.OpenImgDirectory(fixture);
            AssertAsset(fresh.FindImage("Map/Obj", "replacement.img"), 2, Color.Lime);
            var freshServices = new RuntimeDataServices(fresh, new SourceRuntimeAssetCatalog(fresh));
            using var freshProvider = new RuntimeMapProvider(freshServices);
            using RuntimeMapDefinition repaired = freshProvider.Load(20000);
            Assert.Equal(20000, repaired.MapId);
            AssertAsset(detached, 1, Color.Magenta);
        }
        finally
        {
            // Delete only the unique fixture created above, after all source readers close.
            string resolvedFixture = Path.GetFullPath(fixture);
            string allowedPrefix = Path.TrimEndingDirectorySeparator(temporaryRoot) + Path.DirectorySeparatorChar;
            if (!resolvedFixture.StartsWith(allowedPrefix, StringComparison.OrdinalIgnoreCase)
                || !Path.GetFileName(resolvedFixture).StartsWith("MapleGame-external-replacement-", StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing to remove a path outside the test fixture.");
            Directory.Delete(resolvedFixture, recursive: true);
        }
    }

    private static void WriteAsset(string path, int marker, Color color)
    {
        using var image = new WzImage("replacement.img") { Parsed = true, Changed = true };
        image.AddProperty(new WzIntProperty("marker", marker));
        using var bitmap = new Bitmap(3, 2);
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++) bitmap.SetPixel(x, y, color);
        image.AddProperty(new WzCanvasProperty("canvas") { PngProperty = new WzPngProperty { PNG = bitmap } });
        image.AddProperty(new WzRawDataProperty("raw", 0, new byte[] { (byte)marker, 17, 29, 41 }));
        File.WriteAllBytes(path, WzImgSerializer.CreateForImgExtraction().SerializeImage(image));
    }

    private static void WriteMap(string path)
    {
        using var image = new WzImage("000020000.img") { Parsed = true, Changed = true };
        var info = new WzSubProperty("info");
        info.AddProperty(new WzIntProperty("returnMap", 20000));
        info.AddProperty(new WzIntProperty("VRLeft", 0));
        info.AddProperty(new WzIntProperty("VRTop", 0));
        info.AddProperty(new WzIntProperty("VRRight", 800));
        info.AddProperty(new WzIntProperty("VRBottom", 600));
        image.AddProperty(info);
        File.WriteAllBytes(path, WzImgSerializer.CreateForImgExtraction().SerializeImage(image));
    }

    private static void AssertAsset(WzImage image, int marker, Color color)
    {
        Assert.NotNull(image);
        Assert.Equal(marker, ((WzIntProperty)image["marker"]).Value);
        Assert.Equal(new byte[] { (byte)marker, 17, 29, 41 }, ((WzRawDataProperty)image["raw"]).GetBytes(false));
        Bitmap bitmap = ((WzCanvasProperty)image["canvas"]).GetLinkedWzCanvasBitmap();
        Assert.NotNull(bitmap);
        Assert.Equal(3, bitmap.Width);
        Assert.Equal(2, bitmap.Height);
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++) Assert.Equal(color.ToArgb(), bitmap.GetPixel(x, y).ToArgb());
    }
}
