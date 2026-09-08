using HaCreator.WorldMap;
using HaCreator.MapSimulator.WorldMap;
using MapleLib.WzLib.WzProperties;
using System.Drawing;

namespace UnitTest_WorldMapEditor;

public sealed class WorldMapDocumentTests
{
    [Fact]
    public void CreateNew_NormalizesImageNameAndStartsWithCanonicalSurface()
    {
        WorldMapDocument document = WorldMapDocument.CreateNew("WorldMapSynthetic", "Synthetic Root");

        Assert.Equal("WorldMapSynthetic.img", document.ImageName);
        Assert.Equal("WorldMap/WorldMapSynthetic.img", document.ImagePath);
        Assert.Equal("Synthetic Root", document.Surface.LogicalName);
        Assert.True(document.IsNew);
        Assert.True(document.IsDirty);
    }

    [Fact]
    public void Surface_AddOperationsUseFirstUnusedNumericKeyAfterSparseNativeKeys()
    {
        var surface = new WorldMapSurface("Synthetic");
        surface.AddEntry("0");
        surface.AddEntry("2");
        surface.AddLink("0");
        surface.AddFogLayer("1");

        Assert.Equal("1", surface.AddEntry().Key);
        Assert.Equal("1", surface.AddLink().Key);
        Assert.Equal("0", surface.AddFogLayer().Key);
    }

    [Fact]
    public void DeepClone_CopiesEditableListsAndCanvasMetadataWithoutSharingCollections()
    {
        WzCanvasProperty baseCanvas = WorldMapFixtureFactory.CreateCanvas("0", 640, 470);
        baseCanvas.AddProperty(new WzVectorProperty("origin", 320, 235));
        baseCanvas.AddProperty(new WzIntProperty("z", 4));
        var surface = new WorldMapSurface("Synthetic")
        {
            ParentName = "Parent",
            BaseImage = new WorldMapCanvasRef(baseCanvas)
        };
        WorldMapMapEntry entry = surface.AddEntry("7");
        entry.Type = 29;
        entry.Spot = new Point(-119, -26);
        entry.MapIds.Add(WorldMapFixtureFactory.FirstMapId);
        WorldMapLink link = surface.AddLink("3");
        link.LinkMap = WorldMapFixtureFactory.ChildName;
        WorldMapFogLayer fog = surface.AddFogLayer("9");
        fog.Quest = 12345;
        fog.QState = 2;

        WorldMapSurface clone = surface.DeepClone();

        Assert.NotSame(surface, clone);
        Assert.NotSame(surface.Entries, clone.Entries);
        Assert.NotSame(surface.Entries[0], clone.Entries[0]);
        Assert.Equal(surface.Entries[0].Spot, clone.Entries[0].Spot);
        Assert.Equal(surface.Entries[0].MapIds, clone.Entries[0].MapIds);
        clone.Entries[0].MapIds.Add(WorldMapFixtureFactory.SecondMapId);
        Assert.Single(surface.Entries[0].MapIds);
        Assert.Equal(surface.Links[0].LinkMap, clone.Links[0].LinkMap);
        Assert.Equal(surface.FogLayers[0].QState, clone.FogLayers[0].QState);
        Assert.Equal(surface.BaseImage.Origin, clone.BaseImage.Origin);
    }
}
