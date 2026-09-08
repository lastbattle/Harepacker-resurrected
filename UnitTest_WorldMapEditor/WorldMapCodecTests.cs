using HaCreator.WorldMap;
using HaCreator.MapSimulator.WorldMap;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace UnitTest_WorldMapEditor;

public sealed class WorldMapCodecTests
{
    [Fact]
    public void Read_ParsesSparseMarkersLinksFogAndCanvasMetadata()
    {
        WzImage source = WorldMapFixtureFactory.CreateSurface();

        WorldMapDocument document = WorldMapCodec.Read(source);

        Assert.Equal("WorldMapFixture.img", document.ImageName);
        Assert.Equal(WorldMapFixtureFactory.RootName, document.Surface.LogicalName);
        Assert.Null(document.Surface.ParentName);
        Assert.Equal(640, document.Surface.BaseImage!.Width);
        Assert.Equal(470, document.Surface.BaseImage.Height);
        Assert.Equal(320, document.Surface.BaseImage.Origin.X);
        Assert.Equal(235, document.Surface.BaseImage.Origin.Y);
        Assert.Equal(2, document.Surface.Entries.Count);
        Assert.Equal("7", document.Surface.Entries[0].Key);
        Assert.Equal(29, document.Surface.Entries[0].Type);
        Assert.Equal(-119, document.Surface.Entries[0].Spot.X);
        Assert.Equal(new[] { WorldMapFixtureFactory.FirstMapId, WorldMapFixtureFactory.SecondMapId },
            document.Surface.Entries[0].MapIds);
        Assert.Equal("3", Assert.Single(document.Surface.Links).Key);
        Assert.Equal(WorldMapFixtureFactory.ChildName, document.Surface.Links[0].LinkMap);
        Assert.Equal(218, document.Surface.Links[0].LinkImage!.Width);
        WorldMapFogLayer fog = Assert.Single(document.Surface.FogLayers);
        Assert.Equal("9", fog.Key);
        Assert.Equal(12345, fog.Quest);
        Assert.Equal(2, fog.QState);
        Assert.False(document.IsDirty);
        Assert.NotNull(document.RawImage);
    }

    [Fact]
    public void ApplyToClone_PatchesKnownFieldsAndPreservesUnknownPropertiesAndCanvases()
    {
        WzImage source = WorldMapFixtureFactory.CreateSurface();
        WorldMapDocument document = WorldMapCodec.Read(source);
        document.Surface.LogicalName = "FixtureRenamed";
        document.Surface.ParentName = "FixtureParent";
        document.Surface.Entries[0].Title = "Edited title";
        document.Surface.Entries[0].MapIds.Add(300000000);
        document.Surface.Links[0].ToolTip = "Edited tooltip";
        document.Surface.FogLayers[0].QState = 1;

        WzImage saved = WorldMapWriter.ApplyToClone(document);

        Assert.True(saved.Changed);
        Assert.Equal("FixtureRenamed", ((WzStringProperty)saved["info"]["WorldMap"]).Value);
        Assert.Equal("FixtureParent", ((WzStringProperty)saved["info"]["parentMap"]).Value);
        Assert.Equal("preserve-me", ((WzStringProperty)saved["info"]["futureInfoField"]).Value);
        Assert.Equal(7, ((WzIntProperty)saved["futureRoot"]["version"]).Value);
        Assert.IsType<WzNullProperty>(saved["futureRoot"]["marker"]);
        Assert.Equal("Edited title", ((WzStringProperty)saved["MapList"]["7"]["title"]).Value);
        Assert.Equal("preserve-entry", ((WzStringProperty)saved["MapList"]["7"]["futureEntryField"]).Value);
        Assert.Equal(3, saved["MapList"]["7"]["mapNo"].WzProperties.Count);
        Assert.Equal("Edited tooltip", ((WzStringProperty)saved["MapLink"]["3"]["toolTip"]).Value);
        Assert.Equal(73, ((WzIntProperty)saved["MapLink"]["3"]["link"]["futureLinkField"]).Value);
        Assert.Equal(1, ((WzIntProperty)saved["Fog"]["9"]["qState"]).Value);
        Assert.Equal(640, ((WzCanvasProperty)saved["BaseImg"]["0"]).PngProperty.Width);
    }

    [Fact]
    public void ReadExclusions_RecognizesBothNativeExclusionImages()
    {
        WzImage mapExclusions = WorldMapFixtureFactory.CreateExclusionList("SearchExcept.img");
        WzImage npcExclusions = WorldMapFixtureFactory.CreateExclusionList("SearchExceptForNPC.img");

        Assert.True(WorldMapExclusionList.IsExclusionImage("Map/WorldMap/SearchExcept.img"));
        Assert.True(WorldMapExclusionList.IsExclusionImage("SearchExceptForNPC.img"));
        Assert.False(WorldMapExclusionList.IsExclusionImage("WorldMap001.img"));
        WorldMapExclusionList mapList = WorldMapExclusionList.Read(mapExclusions);
        WorldMapExclusionList npcList = WorldMapExclusionList.Read(npcExclusions);
        Assert.Equal(new[]
        {
            WorldMapFixtureFactory.FirstMapId,
            WorldMapFixtureFactory.SecondMapId,
            200000000
        }, mapList.Entries.Select(entry => entry.MapId));
        Assert.Equal(mapList.Entries.Select(entry => entry.MapId), npcList.Entries.Select(entry => entry.MapId));

        mapList.Add(300000000);
        WzImage saved = mapList.Write();
        Assert.Contains(saved.WzProperties, property => property is WzIntProperty intProperty && intProperty.Value == 300000000);

        mapList.Entries[1] = ("9", mapList.Entries[1].MapId);
        WzImage rekeyed = mapList.Write();
        Assert.Null(rekeyed["4"]);
        Assert.Equal(WorldMapFixtureFactory.SecondMapId, ((WzIntProperty)rekeyed["9"]).Value);
    }
}
