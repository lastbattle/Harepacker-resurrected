using HaCreator.MapEditor;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Simulation;
using HaCreator.MapSimulator.Contracts;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using Microsoft.Xna.Framework;
using Point = Microsoft.Xna.Framework.Point;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace UnitTest_MapSimulator;

public class RuntimeMapSnapshotTests
{
    [Fact]
    public void SavedMovementAndSpecialRegionsMatchBoardSnapshot() => OnSta(() =>
    {
        var board = new Board(new Point(200, 100), new Point(100, 50), new MultiBoard(), true,
            null, ItemTypes.All, ItemTypes.All);
        try
        {
            board.CreateMapLayers();
            board.MapInfo.id = 100000001;
            board.BoardItems.Chairs.Add(new HaCreator.MapEditor.Instance.Shapes.Chair(board, 17, 29));
            board.BoardItems.Ropes.Add(new HaCreator.MapEditor.Instance.Shapes.Rope(board, 30, -20, 70, true, 2, true));
            board.BoardItems.MiscItems.Add(new HaCreator.MapEditor.Instance.Misc.Area(
                board, new Rectangle(-10, -20, 40, 60), "safe"));
            board.BoardItems.MiscItems.Add(new HaCreator.MapEditor.Instance.Misc.Clock(
                board, new Rectangle(10, 20, 80, 40)));
            using RuntimeMapDefinition snapshot = new BoardSnapshotBuilder().Create(board);

            // Exercise the actual editor serializers without InsertImage, which writes to
            // the user's configured data source and updates editor dirty state.
            var saver = new HaCreator.Wz.MapSaver(board);
            typeof(HaCreator.Wz.MapSaver).GetMethod("CreateImage",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(saver, null);
            saver.SaveRopes();
            saver.SaveChairs();
            saver.SaveMisc();
            using WzImage saved = saver.MapImage;
            var savedInfo = new WzSubProperty("info");
            savedInfo.AddProperty(new WzIntProperty("VRLeft", -100));
            savedInfo.AddProperty(new WzIntProperty("VRRight", 100));
            savedInfo.AddProperty(new WzIntProperty("VRTop", -50));
            savedInfo.AddProperty(new WzIntProperty("VRBottom", 50));
            saved.AddProperty(savedInfo);
            using RuntimeMapDefinition decoded = new HaCreator.MapSimulator.Assets.WzRuntimeMapReader().Read(
                saved, new HaCreator.MapSimulator.Assets.WzRuntimeMapReaderOptions { MapId = board.MapInfo.id });
            Assert.Equal(snapshot.MapId, decoded.MapId);
            Assert.Equal(snapshot.Chairs, decoded.Chairs);
            Assert.Equal(snapshot.Ropes, decoded.Ropes);
            Assert.Equal(snapshot.Misc.OrderBy(x => x.Kind), decoded.Misc.OrderBy(x => x.Kind));
        }
        finally { board.Dispose(); }
    });

    [Fact]
    public void MissingMinimapIsGeneratedWithoutChangingEditorState() => OnSta(() =>
    {
        var boardMenu = new System.Windows.Controls.ContextMenu();
        boardMenu.Items.Add(new System.Windows.Controls.MenuItem());
        boardMenu.Items.Add(new System.Windows.Controls.MenuItem());
        boardMenu.Items.Add(new System.Windows.Controls.MenuItem());
        var board = new Board(new Point(200, 100), new Point(100, 50), new MultiBoard(), true,
            boardMenu, ItemTypes.All, ItemTypes.All);
        try
        {
            board.CreateMapLayers();
            board.MinimapRectangle = new HaCreator.MapEditor.Instance.Shapes.MinimapRectangle(
                board, new Rectangle(-80, -40, 160, 80));
            board.mag = 4;
            board.MiniMap = null;
            var originalPosition = new System.Drawing.Point(17, 23);
            board.MinimapPosition = originalPosition;

            using RuntimeMapDefinition snapshot = new BoardSnapshotBuilder().Create(board);
            Assert.Null(board.MiniMap);
            Assert.Equal(originalPosition, board.MinimapPosition);
            Assert.Equal(new System.Drawing.Point(-80, -40), snapshot.MinimapPosition);
            using var png = new System.IO.MemoryStream(snapshot.CreateMinimapPngCopy());
            using var image = new System.Drawing.Bitmap(png);
            Assert.Equal(40, image.Width);
            Assert.Equal(20, image.Height);
        }
        finally { board.Dispose(); }
    });

    [Fact]
    public void BoardSnapshotFlattensPortalAndMapInfoBeforeFurtherEdits() => OnSta(() =>
    {
        var board = new Board(new Point(200, 100), new Point(100, 50), new MultiBoard(), true,
            null, ItemTypes.All, ItemTypes.All);
        try
        {
            board.CreateMapLayers();
            board.MapInfo.id = 910000000;
            board.MapInfo.strMapName = "Before";
            var portal = new PortalInstance(null, board, 25, 35, "sp", (PortalType)0, "out", 100000000,
                "script", 15, null, null, null, null, null, null, null, null);
            board.BoardItems.Portals.Add(portal);

            using RuntimeMapDefinition snapshot = new BoardSnapshotBuilder().Create(board);
            portal.pn = "changed";
            portal.tm = 999999999;
            board.MapInfo.strMapName = "After";
            board.BoardItems.Portals.Clear();

            RuntimePortalDefinition saved = Assert.Single(snapshot.Portals);
            Assert.Equal("sp", saved.Name);
            Assert.Equal(100000000, saved.TargetMapId);
            Assert.Equal(25, saved.X);
            Assert.Equal(35, saved.Y);
            Assert.Equal("Before", snapshot.CreateMapInfo().strMapName);
        }
        finally
        {
            board.Dispose();
        }
    });

    [Fact]
    public void CreatingAndDisposingUnsavedSnapshotPreservesDirtyStateAndLiveUndoRedoHistory() => OnSta(() =>
    {
        var board = new Board(new Point(200, 100), new Point(100, 50), new MultiBoard(), true,
            null, ItemTypes.All, ItemTypes.All);
        try
        {
            board.CreateMapLayers();
            board.MapInfo.id = 100000001;
            board.MapInfo.strMapName = "Original name";
            board.MapInfo.help = "Original help";
            board.UndoRedoMan.AddUndoBatch(new()
            {
                HaCreator.MapEditor.UndoRedo.UndoRedoManager.ValueChanged(
                    () => board.MapInfo.strMapName = "Original name",
                    () => board.MapInfo.strMapName = "Unsaved preview name")
            });
            board.MapInfo.strMapName = "Unsaved preview name";
            board.UndoRedoMan.AddUndoBatch(new()
            {
                HaCreator.MapEditor.UndoRedo.UndoRedoManager.ValueChanged(
                    () => board.MapInfo.help = "Original help",
                    () => board.MapInfo.help = "Redo help")
            });
            board.MapInfo.help = "Redo help";
            board.UndoRedoMan.Undo();
            board.Dirty = true;

            var undoList = board.UndoRedoMan.UndoList;
            var redoList = board.UndoRedoMan.RedoList;
            var undoBatch = Assert.Single(undoList);
            var redoBatch = Assert.Single(redoList);
            var undoAction = Assert.Single(undoBatch.Actions);
            var redoAction = Assert.Single(redoBatch.Actions);
            var mapInfo = board.MapInfo;

            void AssertEditorStateUnchanged()
            {
                Assert.True(board.Dirty);
                Assert.Same(mapInfo, board.MapInfo);
                Assert.Equal("Unsaved preview name", board.MapInfo.strMapName);
                Assert.Equal("Original help", board.MapInfo.help);
                Assert.Same(undoList, board.UndoRedoMan.UndoList);
                Assert.Same(redoList, board.UndoRedoMan.RedoList);
                Assert.Same(undoBatch, Assert.Single(undoList));
                Assert.Same(redoBatch, Assert.Single(redoList));
                Assert.Same(undoAction, Assert.Single(undoBatch.Actions));
                Assert.Same(redoAction, Assert.Single(redoBatch.Actions));
            }

            using (RuntimeMapDefinition snapshot = new BoardSnapshotBuilder().Create(board))
            {
                var detachedInfo = snapshot.CreateMapInfo();
                try { Assert.Equal("Unsaved preview name", detachedInfo.strMapName); }
                finally { detachedInfo.Image.Dispose(); }
                AssertEditorStateUnchanged();
            }
            AssertEditorStateUnchanged();

            // The preserved entries must remain executable, including the redo tail.
            board.UndoRedoMan.Redo();
            Assert.Equal("Redo help", board.MapInfo.help);
            board.UndoRedoMan.Undo();
            Assert.Equal("Original help", board.MapInfo.help);
            board.UndoRedoMan.Undo();
            Assert.Equal("Original name", board.MapInfo.strMapName);
            board.UndoRedoMan.Redo();
            Assert.Equal("Unsaved preview name", board.MapInfo.strMapName);
        }
        finally { board.Dispose(); }
    });

    private static void OnSta(Action action)
    {
        Exception failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Runtime map snapshot test timed out.");
        Assert.Null(failure);
    }
}
