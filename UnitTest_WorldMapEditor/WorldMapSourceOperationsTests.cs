using HaCreator.WorldMap;
using HaCreator.MapSimulator.WorldMap;
using MapleLib.Img;
using MapleLib.WzLib;
using System;
using System.IO;

namespace UnitTest_WorldMapEditor;

public sealed class WorldMapSourceOperationsTests
{
    [Fact]
    public void ImgSource_ReportsCapabilitiesAndNormalizesBlankImageName()
    {
        string root = Path.Combine(Path.GetTempPath(), "worldmap-editor-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var source = new ImgFileSystemDataSource(root);
            var operations = new WorldMapSourceOperations(source);

            Assert.Equal(WorldMapSourceMode.Img, operations.Mode);
            Assert.True(operations.Capabilities.CanCreate);
            Assert.True(operations.Capabilities.CanDelete);
            Assert.True(operations.Capabilities.SupportsAtomicBatch);
            Assert.True(operations.Capabilities.WritesImmediately);

            WzImage created = operations.CreateBlank("WorldMapSynthetic");
            Assert.Equal("WorldMapSynthetic.img", created.Name);
            Assert.Empty(operations.EnumerateImageNames());
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void UnknownSourceMode_RejectsCreateRatherThanWritingOutsideSource()
    {
        var source = new InMemoryWorldMapDataSource();
        var operations = new WorldMapSourceOperations(source);

        Assert.Equal(WorldMapSourceMode.Unknown, operations.Mode);
        Assert.False(operations.Capabilities.CanCreate);
        Assert.Throws<InvalidOperationException>(() => operations.CreateBlank("WorldMapSynthetic"));
    }
}
