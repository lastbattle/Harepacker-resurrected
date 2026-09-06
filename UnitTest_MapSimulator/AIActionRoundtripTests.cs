using System.Drawing;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Threading;
using HaCreator;
using HaCreator.MapEditor;
using HaCreator.MapEditor.AI;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Instance.Shapes;
using MapleLib.WzLib.WzStructure.Data;
using Newtonsoft.Json.Linq;
using XnaPoint = Microsoft.Xna.Framework.Point;
using UserSettings = HaSharedLibrary.Configuration.HaCreatorUserSettings;

namespace UnitTest_MapSimulator;

[Collection("AI placement dataset")]
public class AIActionRoundtripTests
{
    [Theory]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    public void DecimalToolValuesRemainInvariantAcrossUiCultures(string culture) => OnBoard((board, executor) =>
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            float original = board.MapInfo.mobRate;
            Call(executor, "set_mob_rate", new JObject { ["rate"] = 2.5 });
            Assert.Equal(2.5f, board.MapInfo.mobRate);
            board.UndoRedoMan.Undo();
            Assert.Equal(original, board.MapInfo.mobRate);
            board.UndoRedoMan.Redo();
            Assert.Equal(2.5f, board.MapInfo.mobRate);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    });

    [Theory]
    [InlineData("set_help", "text")]
    [InlineData("set_map_desc", "description")]
    public void EmptyTextClearsExistingValueAndRestoresOnUndo(string tool, string parameter) => OnBoard((board, executor) =>
    {
        board.MapInfo.help = "original help";
        board.MapInfo.mapDesc = "original description";
        Func<string> read = tool == "set_help" ? () => board.MapInfo.help : () => board.MapInfo.mapDesc;
        string original = read();
        Call(executor, tool, new JObject { [parameter] = "" });
        Assert.Equal("", read());
        board.UndoRedoMan.Undo();
        Assert.Equal(original, read());
        board.UndoRedoMan.Redo();
        Assert.Equal("", read());
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BackgroundMoveHistoryUsesBaseCoordinatesAcrossCameraChanges(bool parallax) => OnBoard((board, executor) =>
    {
        bool previous = UserSettings.emulateParallax;
        try
        {
            UserSettings.emulateParallax = parallax;
            using var pixels = new Bitmap(30, 40);
            var background = (BackgroundInstance)Add(board, "background", pixels, 100, 200);
            background.rx = -50;
            background.ry = 25;
            Call(executor, "move_element", new JObject
            {
                ["element_type"] = "background", ["from_x"] = 100, ["from_y"] = 200,
                ["to_x"] = 300, ["to_y"] = 400
            });
            Assert.Equal((300, 400), Position(background));
            // The harness has no scrollbar widgets; change the underlying camera state.
            typeof(Board).GetField("_hScroll", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(board, 130);
            typeof(Board).GetField("_vScroll", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(board, 70);
            UserSettings.emulateParallax = !parallax;
            board.UndoRedoMan.Undo();
            Assert.Equal((100, 200), Position(background));
            board.UndoRedoMan.Redo();
            Assert.Equal((300, 400), Position(background));
            Call(executor, "remove_element", new JObject { ["element_type"] = "background", ["x"] = 300, ["y"] = 400 });
            Assert.Empty(board.BoardItems.BackBackgrounds);
            board.UndoRedoMan.Undo();
            Assert.Same(background, Assert.Single(board.BoardItems.BackBackgrounds));
            Assert.Equal((300, 400), Position(background));
        }
        finally { UserSettings.emulateParallax = previous; }
    });

    [Theory]
    [InlineData("mob")]
    [InlineData("npc")]
    [InlineData("reactor")]
    [InlineData("portal")]
    [InlineData("background")]
    public void ExactMoveAndRemovePreserveNearbyInstancesAndUndo(string type) => OnBoard((board, executor) =>
    {
        using var pixels = new Bitmap(30, 40);
        var selected = Add(board, type, pixels, 100, 200);
        var neighbor = Add(board, type, pixels, 101, 200);
        var original = Position(selected);
        Call(executor, "move_element", new JObject
        {
            ["element_type"] = type, ["from_x"] = 100, ["from_y"] = 200,
            ["to_x"] = 300, ["to_y"] = 400
        });
        Assert.Equal((300, 400), Position(selected));
        Assert.Equal((101, 200), Position(neighbor));
        board.UndoRedoMan.Undo();
        Assert.Equal(original, Position(selected));
        board.UndoRedoMan.Redo();
        Assert.Equal((300, 400), Position(selected));
        Call(executor, "remove_element", new JObject { ["element_type"] = type, ["x"] = 300, ["y"] = 400 });
        Assert.DoesNotContain(selected, board.BoardItems.Items);
        Assert.Contains(neighbor, board.BoardItems.Items);
        board.UndoRedoMan.Undo();
        Assert.Contains(selected, board.BoardItems.Items);
        Assert.Equal((300, 400), Position(selected));
        board.UndoRedoMan.Redo();
        Assert.DoesNotContain(selected, board.BoardItems.Items);
        Assert.Contains(neighbor, board.BoardItems.Items);
    });

    [Theory]
    [InlineData("mob")]
    [InlineData("npc")]
    [InlineData("reactor")]
    [InlineData("background")]
    public void FlipRestoresAsymmetricOriginAndTargetsExactlyOneInstance(string type) => OnBoard((board, executor) =>
    {
        using var pixels = new Bitmap(30, 40);
        var selected = Add(board, type, pixels, 100, 200);
        var neighbor = Add(board, type, pixels, 101, 200);
        Call(executor, "flip_element", new JObject { ["element_type"] = type, ["x"] = 100, ["y"] = 200 });
        Assert.True(((IFlippable)selected).Flip);
        Assert.False(((IFlippable)neighbor).Flip);
        var flippedPosition = Position(selected);
        Assert.NotEqual((100, 200), flippedPosition);
        board.UndoRedoMan.Undo();
        Assert.False(((IFlippable)selected).Flip);
        Assert.Equal((100, 200), Position(selected));
        board.UndoRedoMan.Redo();
        Assert.True(((IFlippable)selected).Flip);
        Assert.Equal(flippedPosition, Position(selected));
        Assert.Equal((101, 200), Position(neighbor));
    });

    [Theory]
    [InlineData("move_element")]
    [InlineData("remove_element")]
    [InlineData("flip_element")]
    public void AmbiguousCoordinatesFailWithoutMutationOrUndo(string tool) => OnBoard((board, executor) =>
    {
        using var pixels = new Bitmap(30, 40);
        var first = Add(board, "mob", pixels, 100, 200);
        var second = Add(board, "mob", pixels, 100, 200);
        var args = new JObject { ["element_type"] = "mob" };
        if (tool == "move_element")
        {
            args["from_x"] = 100; args["from_y"] = 200; args["to_x"] = 300; args["to_y"] = 400;
        }
        else { args["x"] = 100; args["y"] = 200; }
        Assert.False(CallResult(executor, tool, args).Success);
        Assert.Empty(board.UndoRedoMan.UndoList);
        Assert.Contains(first, board.BoardItems.Items);
        Assert.Contains(second, board.BoardItems.Items);
        Assert.Equal((100, 200), Position(first));
        Assert.Equal((100, 200), Position(second));
        Assert.False(((IFlippable)first).Flip);
        Assert.False(((IFlippable)second).Flip);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RopeRemovalUsesLayerAndRestoresAnchorsAndLine(bool ladder) => OnBoard((board, executor) =>
    {
        var selected = new Rope(board, 100, 0, 200, ladder, 2, true);
        var otherLayer = new Rope(board, 100, 0, 200, ladder, 3, true);
        var otherKind = new Rope(board, 100, 0, 200, !ladder, 2, true);
        board.BoardItems.Ropes.Add(selected);
        board.BoardItems.Ropes.Add(otherLayer);
        board.BoardItems.Ropes.Add(otherKind);
        var args = new JObject { ["element_type"] = ladder ? "ladder" : "rope", ["x"] = 100, ["y"] = 100 };
        Assert.False(CallResult(executor, "remove_element", args).Success);
        Assert.Empty(board.UndoRedoMan.UndoList);
        args["layer"] = 2;
        Call(executor, "remove_element", args);
        Assert.Equal(2, board.BoardItems.Ropes.Count);
        Assert.Equal(4, board.BoardItems.RopeAnchors.Count);
        Assert.Equal(2, board.BoardItems.RopeLines.Count);
        Assert.DoesNotContain(selected, board.BoardItems.Ropes);
        board.UndoRedoMan.Undo();
        Assert.Contains(selected, board.BoardItems.Ropes);
        Assert.Contains(selected.FirstAnchor, board.BoardItems.RopeAnchors);
        Assert.Contains(selected.SecondAnchor, board.BoardItems.RopeAnchors);
        Assert.Equal(6, board.BoardItems.RopeAnchors.Count);
        Assert.Equal(3, board.BoardItems.RopeLines.Count);
        board.UndoRedoMan.Redo();
        Assert.DoesNotContain(selected, board.BoardItems.Ropes);
        Assert.Contains(otherLayer, board.BoardItems.Ropes);
        Assert.Contains(otherKind, board.BoardItems.Ropes);
        Assert.Equal(4, board.BoardItems.RopeAnchors.Count);
        Assert.Equal(2, board.BoardItems.RopeLines.Count);
    });

    private static BoardItem Add(Board board, string type, Bitmap pixels, int x, int y)
    {
        var origin = new Point(4, 9);
        switch (type)
        {
            case "mob":
                var mob = new MobInstance(new MobInfo(pixels, origin, "100100", "test", null), board, x, y,
                    -20, 20, 0, null, null, false, false, null, null);
                board.BoardItems.Mobs.Add(mob); return mob;
            case "npc":
                var npc = new NpcInstance(new NpcInfo(pixels, origin, "1000000", null), board, x, y,
                    -20, 20, 0, null, null, false, false, null, null);
                board.BoardItems.NPCs.Add(npc); return npc;
            case "reactor":
                var reactor = new ReactorInstance(new ReactorInfo(pixels, origin, "0002000", "test", null), board, x, y, 0, "test", false);
                board.BoardItems.Reactors.Add(reactor); return reactor;
            case "background":
                var background = new BackgroundInstance(new BackgroundInfo(null, pixels, origin, "test", (BackgroundInfoType)0, "0", null, null),
                    board, x, y, 0, 0, 0, 0, 0, (BackgroundType)0, 255, false, false, 0, 0, null, false);
                board.BoardItems.BackBackgrounds.Add(background); return background;
            case "portal":
                var portal = new PortalInstance(null, board, x, y, "sp" + x, (PortalType)0, "", 0, null,
                    null, null, null, null, null, null, null, null);
                board.BoardItems.Portals.Add(portal); return portal;
            default: throw new ArgumentOutOfRangeException(nameof(type));
        }
    }

    private static (int, int) Position(BoardItem item) => item is BackgroundInstance background
        ? (background.BaseX, background.BaseY) : (item.X, item.Y);

    private static void Call(MapAIExecutor executor, string tool, JObject arguments)
    {
        var result = CallResult(executor, tool, arguments);
        Assert.True(result.Success, result.Text);
    }

    private static MapMcpToolCallResult CallResult(MapAIExecutor executor, string tool, JObject arguments)
    {
        using var server = new MapMcpToolServer();
        server.CommandExecutor = text =>
        {
            var command = new MapAIParser().ParseCommand(text);
            return command.IsValid && executor.ExecuteCommand(command) ? "Applied" : "# ERROR: " + string.Join("\n", executor.ExecutionLog);
        };
        return server.CallTool(tool, arguments);
    }

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
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "AI action harness timed out");
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
