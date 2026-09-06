using System.Threading;
using HaCreator;
using HaCreator.MapEditor;
using HaCreator.MapEditor.AI;
using HaCreator.MapEditor.Instance;
using HaCreator.Wz;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using XnaPoint = Microsoft.Xna.Framework.Point;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;

namespace UnitTest_MapSimulator;

[Collection("AI placement dataset")]
public class AIMapPropertyUndoTests
{
    [Fact]
    public void ExposedScalarSettingsRoundTripWithoutTouchingOtherFields() => OnBoard((board, executor) =>
    {
        var info = board.MapInfo;
        info.help = "existing help";
        RoundTrip(board, executor, Command(CommandType.SetMapOption, ("option", "town"), ("value", true)), () => info.town, true);
        RoundTrip(board, executor, Command(CommandType.SetMapOption, ("option", "snow"), ("value", true)), () => info.snow, (MapleBool)true);
        Assert.False(info.snow.HasValue);
        RoundTrip(board, executor, Command(CommandType.SetFieldLimit, ("limit", "Unable_To_Jump"), ("enabled", true)), () => info.fieldLimit, 1L);
        RoundTrip(board, executor, Command(CommandType.SetReturnMap, ("return", 100000000), ("forced", 200000000)),
            () => (info.returnMap, info.forcedReturn), (100000000, 200000000));
        RoundTrip(board, executor, Command(CommandType.SetMobRate, ("rate", 2.5f)), () => info.mobRate, 2.5f);
        RoundTrip(board, executor, Command(CommandType.SetLevelLimit, ("min", 30), ("force", 100)),
            () => (info.lvLimit, info.lvForceMove), ((int?)30, (int?)100));
        RoundTrip(board, executor, Command(CommandType.SetMapDesc, ("desc", "new description")), () => info.mapDesc, "new description");
        Assert.Equal("existing help", info.help);
        RoundTrip(board, executor, Command(CommandType.SetHelp, ("text", "new help")), () => info.help, "new help");
        Assert.Equal("existing help", info.help);

        // Nested collapse must retain the newest state on redo when one field changes repeatedly.
        Execute(executor, Command(CommandType.SetHelp, ("text", "B")));
        Execute(executor, Command(CommandType.SetHelp, ("text", "C")));
        board.UndoRedoMan.CollapseUndoBatches(0);
        Execute(executor, Command(CommandType.SetHelp, ("text", "D")));
        board.UndoRedoMan.CollapseUndoBatches(0);
        Assert.Single(board.UndoRedoMan.UndoList);
        board.UndoRedoMan.Undo();
        Assert.Equal("existing help", info.help);
        board.UndoRedoMan.Redo();
        Assert.Equal("D", info.help);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BgmUndoPreservesTypedNullAndUnrelatedAudio(bool initiallyAbsent) => OnBoard((board, executor) =>
    {
        var previous = Program.InfoManager;
        try
        {
            Program.InfoManager = new WzInformationManager();
            Program.InfoManager.BGMs["Bgm00/B"] = null;
            Program.InfoManager.BGMs["Bgm00/C"] = null;
            var info = board.MapInfo;
            info.bgm = "legacy-A";
            var originalAudio = initiallyAbsent ? null : new MapAudioInfo { PrimaryBgm = null!, AmbientBgm = "ambient", AmbientVolume = 35 };
            info.audio = originalAudio!;
            Execute(executor, Command(CommandType.SetBgm, ("bgm", "Bgm00/B")));
            Execute(executor, Command(CommandType.SetBgm, ("bgm", "Bgm00/C")));
            board.UndoRedoMan.CollapseUndoBatches(0);
            board.UndoRedoMan.Undo();
            Assert.Equal("legacy-A", info.bgm);
            Assert.Same(originalAudio, info.audio);
            Assert.Null(info.audio?.PrimaryBgm);
            board.UndoRedoMan.Redo();
            Assert.Equal("Bgm00/C", info.bgm);
            Assert.Equal("Bgm00/C", info.audio!.PrimaryBgm);
            if (!initiallyAbsent)
            {
                Assert.Same(originalAudio, info.audio);
                Assert.Equal("ambient", info.audio.AmbientBgm);
                Assert.Equal(35, info.audio.AmbientVolume);
            }
        }
        finally { Program.InfoManager = previous; }
    });

    [Fact]
    public void MapDimensionsAndVrRestoreTheirLifecycle() => OnBoard((board, executor) =>
    {
        RoundTrip(board, executor, Command(CommandType.SetMapSize, ("width", 1800), ("height", 1200)),
            () => board.MapSize, new XnaPoint(1800, 1200));
        int originalDots = board.BoardItems.SpecialDots.Count;
        var first = Command(CommandType.SetVR, ("left", -100), ("top", -200), ("right", 600), ("bottom", 400));
        Execute(executor, first);
        var expected = new XnaRectangle(-100, -200, 700, 600);
        Assert.Equal(expected, Bounds(board));
        int dotsWithVr = board.BoardItems.SpecialDots.Count;
        Assert.Equal(originalDots + 4, dotsWithVr);
        board.UndoRedoMan.Undo();
        Assert.Null(board.VRRectangle);
        Assert.Equal(originalDots, board.BoardItems.SpecialDots.Count);
        board.UndoRedoMan.Redo();
        Assert.Equal(expected, Bounds(board));
        Assert.Equal(dotsWithVr, board.BoardItems.SpecialDots.Count);
        Execute(executor, Command(CommandType.SetVR, ("left", -20), ("top", -30), ("right", 400), ("bottom", 300)));
        board.UndoRedoMan.Undo();
        Assert.Equal(expected, Bounds(board));
        Assert.Equal(dotsWithVr, board.BoardItems.SpecialDots.Count);
    });

    [Fact]
    public void PortalFieldsRestoreOnTheSameInstance() => OnBoard((board, executor) =>
    {
        var portal = new PortalInstance(null, board, 0, 0, "sp", (PortalType)0, "old-target", 100, null,
            null, null, null, null, null, null, null, null);
        board.BoardItems.Portals.Add(portal);
        var command = Command(CommandType.Modify, ("target_map", 200), ("target_name", "new-target"), ("script", "new-script"));
        command.ElementType = ElementType.Portal;
        command.TargetIdentifier = "sp";
        RoundTrip(board, executor, command, () => (portal.tm, portal.tn, portal.script), (200, "new-target", "new-script"));
        Assert.Same(portal, Assert.Single(board.BoardItems.Portals));
        Assert.Equal("sp", portal.pn);
    });

    private static XnaRectangle? Bounds(Board board) => board.VRRectangle == null ? null :
        new XnaRectangle(board.VRRectangle.Left, board.VRRectangle.Top, board.VRRectangle.Width, board.VRRectangle.Height);

    private static void RoundTrip<T>(Board board, MapAIExecutor executor, MapAICommand command, Func<T> read, T expected)
    {
        T before = read();
        int count = board.UndoRedoMan.UndoList.Count;
        Execute(executor, command);
        Assert.Equal(expected, read());
        Assert.Equal(count + 1, board.UndoRedoMan.UndoList.Count);
        board.UndoRedoMan.Undo();
        Assert.Equal(before, read());
        board.UndoRedoMan.Redo();
        Assert.Equal(expected, read());
        board.UndoRedoMan.Undo();
        Assert.Equal(before, read());
    }

    private static void Execute(MapAIExecutor executor, MapAICommand command) =>
        Assert.True(executor.ExecuteCommand(command), string.Join("\n", executor.ExecutionLog));

    private static MapAICommand Command(CommandType type, params (string key, object value)[] parameters) => new()
    {
        Type = type, IsValid = true, OriginalText = type.ToString(),
        Parameters = parameters.ToDictionary(pair => pair.key, pair => pair.value)
    };

    private static void OnBoard(Action<Board, MapAIExecutor> action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var parent = new MultiBoard();
                var menu = new System.Windows.Controls.ContextMenu();
                for (int i = 0; i < 3; i++) menu.Items.Add(new System.Windows.Controls.MenuItem());
                var board = new Board(new XnaPoint(1200, 800), new XnaPoint(600, 400), parent, true, menu, ItemTypes.All, ItemTypes.All);
                board.CreateMapLayers();
                action(board, new MapAIExecutor(board));
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Property undo harness timed out");
        Assert.Null(failure);
    }
}
