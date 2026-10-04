using System.Drawing;
using System.Threading;
using HaCreator.MapEditor;
using HaCreator.MapEditor.AI;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using MapleLib.WzLib.WzStructure.Data;
using Newtonsoft.Json.Linq;
using XnaPoint = Microsoft.Xna.Framework.Point;

namespace UnitTest_MapSimulator;

[Collection("AI placement dataset")]
public class AISpatialCoverageTests
{
    [Fact]
    public void EmptyMapReportsCanvasAndViewingRangeAfterResize() => OnSta(() =>
    {
        var board = CreateBoard();
        var serializer = new MapAISerializer(board);
        var initial = JObject.Parse(serializer.GenerateSpatialState(new JObject()));
        Assert.Equal(new long[] { -100, -50, 100, 50 }, initial["worldMapBounds"]!.Values<long>());
        Assert.Equal(JTokenType.Null, initial["viewingRange"]!.Type);
        Assert.Equal(JTokenType.Null, initial["globalGeometryBounds"]!.Type);

        board.MapSize = new XnaPoint(600, 400);
        board.VRRectangle = new HaCreator.MapEditor.Instance.Shapes.VRRectangle(board,
            new Microsoft.Xna.Framework.Rectangle(-80, -40, 500, 300));
        var resized = JObject.Parse(serializer.GenerateSpatialState(new JObject { ["element_type"] = "reactor" }));
        Assert.Equal(new long[] { -100, -50, 500, 350 }, resized["worldMapBounds"]!.Values<long>());
        Assert.Equal(new long[] { -80, -40, 420, 260 }, resized["viewingRange"]!.Values<long>());
        Assert.Equal(JTokenType.Null, resized["globalGeometryBounds"]!.Type);
        Assert.Empty((JArray)resized["elements"]!);
    });

    [Fact]
    public void ReactorsHaveNativeBoundsAndBackgroundsNeverExpandWorldGeometry() => OnSta(() =>
    {
        var board = CreateBoard();
        using var art = new Bitmap(10, 10);
        var reactor = new ReactorInfo(art, new Point(2, 3), "0000001", "test", null);
        board.BoardItems.Add(new ReactorInstance(reactor, board, 100, 50, 12, "switch", true), true);
        AddBackground(board, art, 1000000, 0, 0, BackgroundType.Regular);
        var state = JObject.Parse(new MapAISerializer(board).GenerateSpatialState(new JObject()));
        Assert.Equal(1, (int)state["totalGeometryElements"]!);
        Assert.Equal(2, (int)state["totalElements"]!);
        Assert.Equal("[92,47,102,57]", state["globalGeometryBounds"]!.ToString(Newtonsoft.Json.Formatting.None));
        var elements = (JArray)state["elements"]!;
        Assert.Equal("0000001", (string)elements[0]["id"]!);
        Assert.Equal("switch", (string)elements[0]["name"]!);
        Assert.Equal(12, (int)elements[0]["reactorTime"]!);
        Assert.Equal(JTokenType.Null, elements[1]["bounds"]!.Type);
        Assert.Equal(JTokenType.Null, elements[1]["cameraBounds"]!.Type);
        var cropped = JObject.Parse(new MapAISerializer(board).GenerateSpatialState(new JObject
            { ["element_type"] = "reactor", ["x"] = 90, ["y"] = 45, ["width"] = 5, ["height"] = 5 }));
        Assert.Equal(1, (int)cropped["matched"]!);
    });

    [Theory]
    [InlineData(0, 48)]
    [InlineData(-100, 18)]
    public void BackgroundCameraMappingUsesStoredFlippedAnchor(int rx, int expectedLeft) => OnSta(() =>
    {
        var board = CreateBoard();
        using var art = new Bitmap(10, 10);
        AddBackground(board, art, 6, rx, 0, BackgroundType.Regular, true);
        var state = QueryCamera(board);
        var entry = state["elements"]![0]!;
        Assert.Equal(expectedLeft, (double)entry["cameraBounds"]![0]!);
        Assert.Equal(25, (double)entry["cameraBounds"]![1]!);
        Assert.Equal(0, (int)entry["baseX"]!);
        Assert.Equal(6, (int)entry["unflippedX"]!);
        Assert.Equal(JTokenType.Null, state["globalGeometryBounds"]!.Type);
    });

    [Fact]
    public void CropsIncludeRepeatedCopiesButExcludeGapsAndOffscreenRegularBackgrounds() => OnSta(() =>
    {
        var board = CreateBoard();
        using var art = new Bitmap(10, 10);
        AddBackground(board, art, 10000, 0, 0, BackgroundType.Regular);
        AddBackground(board, art, 10000, 0, 0, BackgroundType.HVTiling);
        AddBackground(board, art, 200, 0, 1000, BackgroundType.HorizontalTiling);
        var state = QueryCamera(board);
        Assert.Equal(1, (int)state["matched"]!);
        Assert.Equal(1, (int)state["elements"]![0]!["sourceIndex"]!);
        Assert.Equal(0, (int)state["elements"]![0]!["alpha"]!);
        Assert.Equal(3, (int)state["backgroundElements"]!);
    });

    private static void AddBackground(Board board, Bitmap art, int x, int rx, int cx, BackgroundType type, bool flip = false)
    {
        var info = new BackgroundInfo(null, art, new Point(2, 0), "synthetic", BackgroundInfoType.Background, "0", null, null);
        board.BoardItems.Add(info.CreateInstance(board, x, 0, 0, rx, 0, cx, 0, type, 0, false, flip, 0, 0, null, false), true);
    }

    private static JObject QueryCamera(Board board) => JObject.Parse(new MapAISerializer(board).GenerateSpatialState(new JObject
        { ["element_type"] = "background", ["x"] = -20, ["y"] = -25, ["width"] = 100, ["height"] = 50 }));

    private static Board CreateBoard()
    {
        var menu = new System.Windows.Controls.ContextMenu();
        for (int i = 0; i < 3; i++) menu.Items.Add(new System.Windows.Controls.MenuItem());
        var board = new Board(new XnaPoint(200, 100), new XnaPoint(100, 50), new MultiBoard(), true, menu, ItemTypes.All, ItemTypes.All);
        board.CreateMapLayers();
        return board;
    }

    private static void OnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Spatial coverage test timed out");
        Assert.Null(failure);
    }
}
