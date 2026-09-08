using HaCreator.WorldMap;
using HaCreator.MapSimulator.WorldMap;
using System.Drawing;

namespace UnitTest_WorldMapEditor;

public sealed class WorldMapTransactionAdvancedTests
{
    [Fact]
    public void Commit_FailureOnSecondImage_RestoresFirstImage()
    {
        var source = new InMemoryWorldMapDataSource();
        source.AddImage("Map", "WorldMap/WorldMapFixture.img", WorldMapFixtureFactory.CreateSurface());
        source.AddImage("Map", "WorldMap/WorldMapFixtureChild.img", WorldMapFixtureFactory.CreateChildSurface());
        WorldMapDocument root = WorldMapCodec.Read(source.GetImage("Map", "WorldMap/WorldMapFixture.img")!);
        WorldMapDocument child = WorldMapCodec.Read(source.GetImage("Map", "WorldMap/WorldMapFixtureChild.img")!);
        root.Surface.Entries[0].Title = "first candidate";
        child.Surface.Entries[0].Title = "second candidate";
        source.FailOnSaveNumber = 2;
        var service = new WorldMapTransactionService(new WorldMapSourceOperations(source), new[] { root, child });

        WorldMapTransactionResult result = service.Commit(new[] { root, child });

        Assert.False(result.Succeeded);
        Assert.True(result.RolledBack);
        WorldMapDocument restored = WorldMapCodec.Read(source.GetImage("Map", "WorldMap/WorldMapFixture.img")!);
        Assert.Equal("Fixture Town", restored.Surface.Entries[0].Title);
    }

    [Fact]
    public void RenameAndReparent_RewriteReciprocalNativeReferences()
    {
        WorldMapDocument root = WorldMapCodec.Read(WorldMapFixtureFactory.CreateSurface());
        WorldMapDocument child = WorldMapCodec.Read(WorldMapFixtureFactory.CreateChildSurface());
        WorldMapDocument alternate = WorldMapDocument.CreateNew("WorldMapAlternate", "Alternate");
        var service = new WorldMapTransactionService(new WorldMapSourceOperations(new InMemoryWorldMapDataSource()),
            new[] { root, child, alternate });

        int renameCount = service.RenameLogical(child, "RenamedChild", new[] { root, child, alternate });
        Assert.Equal(1, renameCount);
        Assert.Contains(root.Surface.Links, link => link.LinkMap == "RenamedChild");

        int changed = service.Reparent(child, "Alternate", new[] { root, child, alternate });
        Assert.Equal(2, changed);
        Assert.Equal("Alternate", child.Surface.ParentName);
        Assert.DoesNotContain(root.Surface.Links, link => link.LinkMap == "RenamedChild");
        Assert.Contains(alternate.Surface.Links, link => link.LinkMap == "RenamedChild");
    }

    [Fact]
    public void SemanticComparer_ReportsEditableFieldPathsOnly()
    {
        WorldMapDocument before = WorldMapCodec.Read(WorldMapFixtureFactory.CreateSurface());
        WorldMapDocument after = before.DeepClone();
        after.Surface.Entries[0].Spot = new Point(-100, -20);
        after.Surface.Links[0].ToolTip = "new tooltip";

        WorldMapSemanticReport report = WorldMapSemanticComparer.Compare(before, after);

        Assert.False(report.IsEquivalent);
        Assert.Contains(report.Changes, change => change.Path == "MapList/0/spot");
        Assert.Contains(report.Changes, change => change.Path == "MapLink/0/toolTip");
    }

    [Fact]
    public void DraftLayout_IsDeterministicRowMajorAndPreservesDistinctIds()
    {
        IReadOnlyDictionary<int, Point> layout = WorldMapDraftLayout.Suggest(
            new[] { 101, 102, 103 }, columns: 2, originX: 10, originY: 20, spacingX: 96, spacingY: 72);

        Assert.Equal(new Point(10, 20), layout[101]);
        Assert.Equal(new Point(106, 20), layout[102]);
        Assert.Equal(new Point(10, 92), layout[103]);
        Assert.Equal(3, layout.Count);
    }
}
