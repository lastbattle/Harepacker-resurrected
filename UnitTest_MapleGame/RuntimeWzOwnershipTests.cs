using System.Reflection;
using System.IO;
using HaCreator.MapSimulator.Assets;
using MapleLib;
using MapleLib.Img;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace UnitTest_MapleGame;

public sealed class RuntimeWzOwnershipTests
{
    [Fact]
    public void ExplicitIvReopensSerializedArchiveWithoutGlobalKeyConfiguration()
    {
        string path = CreateDirectory();
        try
        {
            WriteArchive(path, WzMapleVersion.GMS, 73);
            byte[] iv = MapleLib.WzLib.Util.WzTool.GetIvByMapleVersion(WzMapleVersion.GMS).ToArray();
            using RuntimeAssetSource source = RuntimeAssetSourceFactory.OpenWzDirectory(
                path, WzMapleVersion.CUSTOM, customIv: iv);
            Array.Fill(iv, (byte)0);
            Assert.Equal(73, ReadValue(source));
        }
        finally { DeleteDirectory(path); }
    }

    [Theory]
    [InlineData(WzMapleVersion.BMS, false)]
    [InlineData(WzMapleVersion.GMS, false)]
    [InlineData(WzMapleVersion.GMS, true)]
    public void SerializedArchivesReopenIndependently(WzMapleVersion encryption, bool hybrid)
    {
        string firstPath = CreateDirectory();
        string secondPath = CreateDirectory();
        string imgPath = CreateDirectory();
        WzFileManager previousGlobal = WzFileManager.fileManager;
        try
        {
            WriteArchive(firstPath, encryption, 11);
            WriteArchive(secondPath, encryption, 22);
            using RuntimeAssetSource first = hybrid
                ? RuntimeAssetSourceFactory.OpenHybridDirectory(imgPath, firstPath, encryption)
                : RuntimeAssetSourceFactory.OpenWzDirectory(firstPath, encryption);
            using RuntimeAssetSource second = RuntimeAssetSourceFactory.OpenWzDirectory(secondPath, encryption);
            Assert.Same(previousGlobal, WzFileManager.fileManager);
            Assert.Equal(11, ReadValue(first));
            Assert.Equal(22, ReadValue(second));
            first.Dispose();
            Assert.Equal(22, ReadValue(second));
            Assert.Same(previousGlobal, WzFileManager.fileManager);
        }
        finally
        {
            DeleteDirectory(firstPath);
            DeleteDirectory(secondPath);
            DeleteDirectory(imgPath);
        }
    }

    private static int ReadValue(RuntimeAssetSource source)
    {
        WzImage image = source.FindImage("Etc", "RuntimeFixture.img");
        Assert.NotNull(image);
        return Assert.IsType<WzIntProperty>(image["value"]).Value;
    }

    private static void WriteArchive(string directory, WzMapleVersion encryption, int value)
    {
        using var file = new WzFile(95, encryption) { Name = "Etc.wz" };
        var image = new WzImage("RuntimeFixture.img") { Changed = true };
        image.AddProperty(new WzIntProperty("value", value));
        file.WzDirectory.AddImage(image);
        file.SaveToDisk(Path.Combine(directory, "Etc.wz"), false, encryption);
    }

    [Fact]
    public void WzFactoryPreservesEditorGlobalAndDisposesOwnedManager()
    {
        string editorPath = CreateDirectory();
        string runtimePath = CreateDirectory();
        WzFileManager previousGlobal = WzFileManager.fileManager;
        WzFileManager editorManager = new(editorPath, bIsStandAloneWzFile: false);
        WzFile sessionFile = null;
        try
        {
            using RuntimeAssetSource assets = RuntimeAssetSourceFactory.OpenWzDirectory(runtimePath);
            Assert.Same(editorManager, WzFileManager.fileManager);
            Assert.Equal(RuntimeAssetSourceOwnership.Owned, assets.Ownership);

            WzFileDataSource source = GetWzSource(assets);
            sessionFile = CreateImageFile("Map", "100000000.img");
            source.WzManager.LoadWzFile("Map", sessionFile);

            assets.Dispose();
            Assert.True(sessionFile.IsUnloaded);
            Assert.Same(editorManager, WzFileManager.fileManager);
        }
        finally
        {
            editorManager.Dispose();
            WzFileManager.fileManager = previousGlobal;
            DeleteDirectory(editorPath);
            DeleteDirectory(runtimePath);
        }
    }

    [Fact]
    public void HybridFactoryOwnsFallbackManagerWithoutReplacingEditorGlobal()
    {
        string editorPath = CreateDirectory();
        string imgPath = CreateDirectory();
        string wzPath = CreateDirectory();
        Directory.CreateDirectory(Path.Combine(imgPath, "String"));
        WzFileManager previousGlobal = WzFileManager.fileManager;
        WzFileManager editorManager = new(editorPath, bIsStandAloneWzFile: false);
        WzFile sessionFile = null;
        try
        {
            using RuntimeAssetSource assets = RuntimeAssetSourceFactory.OpenHybridDirectory(imgPath, wzPath);
            Assert.Same(editorManager, WzFileManager.fileManager);
            Assert.Equal(RuntimeAssetSourceOwnership.Owned, assets.Ownership);

            HybridDataSource hybrid = GetHybridSource(assets);
            Assert.NotNull(hybrid.WzSource);
            sessionFile = CreateImageFile("Skill", "000000.img");
            hybrid.WzSource.WzManager.LoadWzFile("Skill", sessionFile);

            assets.Dispose();
            Assert.True(sessionFile.IsUnloaded);
            Assert.Same(editorManager, WzFileManager.fileManager);
        }
        finally
        {
            editorManager.Dispose();
            WzFileManager.fileManager = previousGlobal;
            DeleteDirectory(editorPath);
            DeleteDirectory(imgPath);
            DeleteDirectory(wzPath);
        }
    }

    [Fact]
    public void CustomIvRequiresExplicitFourByteKey()
    {
        string path = CreateDirectory();
        WzFileManager previousGlobal = WzFileManager.fileManager;
        try
        {
            Assert.Throws<ArgumentException>(() => RuntimeAssetSourceFactory.OpenWzDirectory(
                path,
                WzMapleVersion.CUSTOM));

            using RuntimeAssetSource assets = RuntimeAssetSourceFactory.OpenWzDirectory(
                path,
                WzMapleVersion.CUSTOM,
                customIv: new byte[] { 1, 2, 3, 4 });
            Assert.Equal("CUSTOM", assets.Version.Encryption);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, GetWzSource(assets).CustomIv);
            Assert.Same(previousGlobal, WzFileManager.fileManager);
        }
        finally
        {
            WzFileManager.fileManager = previousGlobal;
            DeleteDirectory(path);
        }
    }

    private static WzFileDataSource GetWzSource(RuntimeAssetSource assets)
    {
        FieldInfo field = typeof(RuntimeAssetSource).GetField(
            "_source",
            BindingFlags.Instance | BindingFlags.NonPublic);
        return Assert.IsType<WzFileDataSource>(field?.GetValue(assets));
    }

    private static HybridDataSource GetHybridSource(RuntimeAssetSource assets)
    {
        FieldInfo field = typeof(RuntimeAssetSource).GetField(
            "_source",
            BindingFlags.Instance | BindingFlags.NonPublic);
        return Assert.IsType<HybridDataSource>(field?.GetValue(assets));
    }

    private static WzFile CreateImageFile(string name, string imageName)
    {
        WzFile file = new(0, WzMapleVersion.BMS) { Name = name };
        file.WzDirectory.AddImage(new WzImage(imageName));
        return file;
    }

    private static string CreateDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "HarepackerRuntimeWz-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }
}
