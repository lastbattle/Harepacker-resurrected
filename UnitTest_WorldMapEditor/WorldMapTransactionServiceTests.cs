using HaCreator.WorldMap;
using HaCreator.MapSimulator.WorldMap;

namespace UnitTest_WorldMapEditor;

public sealed class WorldMapTransactionServiceTests
{
    [Fact]
    public void Commit_VerifiesSemanticCandidateAndMarksDocumentsClean()
    {
        var source = new InMemoryWorldMapDataSource();
        source.AddImage("Map", "WorldMap/WorldMapFixture.img", WorldMapFixtureFactory.CreateSurface());
        WorldMapDocument document = WorldMapCodec.Read(source.GetImage("Map", "WorldMap/WorldMapFixture.img")!);
        document.Surface.Entries[0].Title = "transaction title";
        var service = new WorldMapTransactionService(new WorldMapSourceOperations(source), new[] { document });

        WorldMapTransactionResult result = service.Commit(new[] { document });

        Assert.True(result.Succeeded);
        Assert.Contains("WorldMap/WorldMapFixture.img", result.AffectedImages);
        Assert.False(document.IsDirty);
        WorldMapDocument reopened = WorldMapCodec.Read(source.GetImage("Map", "WorldMap/WorldMapFixture.img")!);
        Assert.Equal("transaction title", reopened.Surface.Entries[0].Title);
    }

    [Fact]
    public void Commit_BlocksValidationErrorsBeforeSaving()
    {
        var source = new InMemoryWorldMapDataSource();
        source.AddImage("Map", "WorldMap/WorldMapFixture.img", WorldMapFixtureFactory.CreateSurface());
        WorldMapDocument document = WorldMapCodec.Read(source.GetImage("Map", "WorldMap/WorldMapFixture.img")!);
        document.Surface.Entries[0].MapIds[0] = 0;
        var service = new WorldMapTransactionService(new WorldMapSourceOperations(source));

        WorldMapTransactionResult result = service.Commit(new[] { document });

        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, error => error.Contains("outside the native 9-digit range"));
        WorldMapDocument unchanged = WorldMapCodec.Read(source.GetImage("Map", "WorldMap/WorldMapFixture.img")!);
        Assert.Equal(WorldMapFixtureFactory.FirstMapId, unchanged.Surface.Entries[0].MapIds[0]);
    }

    [Fact]
    public void CreateChild_WiresParentAndReciprocalNavigationLink()
    {
        WorldMapDocument parent = WorldMapCodec.Read(WorldMapFixtureFactory.CreateSurface());
        var service = new WorldMapTransactionService(new WorldMapSourceOperations(new InMemoryWorldMapDataSource()));

        WorldMapDocument child = service.CreateChild(parent, "WorldMapNewChild", "NewChild");

        Assert.Equal(WorldMapFixtureFactory.RootName, child.Surface.ParentName);
        Assert.True(child.IsNew);
        Assert.Contains(parent.Surface.Links, link => link.LinkMap == "NewChild");
        Assert.True(parent.IsDirty);
    }
}
