using HaCreator.WorldMap;
using HaCreator.MapSimulator.WorldMap;
using System.Drawing;

namespace UnitTest_WorldMapEditor;

public sealed class WorldMapEditSessionTests
{
    [Fact]
    public void RecordUndoRedo_CoordinatesAreReversibleAndDirtyStateTracksCheckpoint()
    {
        WorldMapDocument document = WorldMapCodec.Read(WorldMapFixtureFactory.CreateSurface());
        Point original = document.Surface.Entries[0].Spot;
        var session = new WorldMapEditSession(document);

        WorldMapChangeSet change = session.Record("Move marker", current =>
            current.Surface.Entries[0].Spot = new Point(original.X + 16, original.Y - 8));

        Assert.Equal("Move marker", change.Description);
        Assert.True(session.CanUndo);
        Assert.False(session.CanRedo);
        Assert.True(session.IsDirty);
        Assert.Equal(original.X + 16, document.Surface.Entries[0].Spot.X);

        Assert.True(session.Undo());
        Assert.Equal(original, document.Surface.Entries[0].Spot);
        Assert.False(session.IsDirty);
        Assert.True(session.CanRedo);

        Assert.True(session.Redo());
        Assert.Equal(original.X + 16, document.Surface.Entries[0].Spot.X);
        Assert.True(session.IsDirty);

        session.MarkSaved();
        Assert.False(session.IsDirty);
        Assert.False(document.IsDirty);
    }

    [Fact]
    public void NewEditAfterUndo_ClearsRedoStackAndKeepsSnapshotsIndependent()
    {
        WorldMapDocument document = WorldMapCodec.Read(WorldMapFixtureFactory.CreateSurface());
        var session = new WorldMapEditSession(document);
        session.Record("first", current => current.Surface.Entries[0].Title = "first");
        session.Record("second", current => current.Surface.Entries[0].Title = "second");

        Assert.True(session.Undo());
        Assert.Equal("first", document.Surface.Entries[0].Title);
        session.Record("replacement", current => current.Surface.Entries[0].Title = "replacement");

        Assert.False(session.CanRedo);
        Assert.Equal("replacement", document.Surface.Entries[0].Title);
        Assert.True(session.Undo());
        Assert.Equal("first", document.Surface.Entries[0].Title);
    }
}
