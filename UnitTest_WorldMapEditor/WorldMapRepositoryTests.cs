using HaCreator.WorldMap;
using HaCreator.MapSimulator.WorldMap;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace UnitTest_WorldMapEditor;

public sealed class WorldMapRepositoryTests
{
    [Fact]
    public void Save_VerifiesReloadAndClearsDirtyState()
    {
        var source = new InMemoryWorldMapDataSource();
        source.AddImage("Map", "WorldMap/WorldMapFixture.img", WorldMapFixtureFactory.CreateSurface());
        var repository = new WorldMapRepository(source);
        WorldMapDocument document = repository.Load("WorldMapFixture.img");
        document.Surface.Entries[0].Title = "Saved title";
        document.IsDirty = true;

        WorldMapBatchSaveResult result = repository.Save(document);

        Assert.True(result.Succeeded);
        Assert.Contains("WorldMap/WorldMapFixture.img", result.AffectedImages);
        Assert.Empty(result.Errors);
        Assert.False(document.IsDirty);
        Assert.False(document.IsNew);
        WorldMapDocument reopened = repository.Load("WorldMapFixture.img");
        Assert.Equal("Saved title", reopened.Surface.Entries[0].Title);
    }

    [Fact]
    public void Save_BlocksExternalRevisionChangesBeforeWritingCandidate()
    {
        var source = new InMemoryWorldMapDataSource();
        source.AddImage("Map", "WorldMap/WorldMapFixture.img", WorldMapFixtureFactory.CreateSurface());
        var repository = new WorldMapRepository(source);
        WorldMapDocument document = repository.Load("WorldMapFixture.img");
        document.Surface.Entries[0].Title = "Local edit";

        WzImage external = WorldMapFixtureFactory.CreateSurface();
        external.AddProperty(new WzStringProperty("externalRevision", "changed"));
        source.AddImage("Map", "WorldMap/WorldMapFixture.img", external);

        WorldMapBatchSaveResult result = repository.Save(document);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("External revision conflict"));
        Assert.Equal("Local edit", document.Surface.Entries[0].Title);
    }
}
