using System.Drawing;
using System.IO;
using System.Threading;
using HaCreator.MapEditor;
using HaCreator.MapEditor.AI;
using HaCreator.MapEditor.Info;
using MapleLib.WzLib.WzStructure.Data;
using Newtonsoft.Json.Linq;
using XnaPoint = Microsoft.Xna.Framework.Point;

namespace UnitTest_MapSimulator;

[Collection("AI placement dataset")]
public class AIVisualRendererTests
{
    [Fact]
    public void BackgroundsRepeatBehindArtworkAndForegroundBlendsOverIt() => OnSta(() =>
    {
        var board = CreateBoard();
        using var blue = new Bitmap(10, 10);
        using (var g = Graphics.FromImage(blue)) g.Clear(Color.Blue);
        var back = new BackgroundInfo(null, blue, Point.Empty, "synthetic", BackgroundInfoType.Background, "0", null, null);
        board.BoardItems.Add(back.CreateInstance(board, 0, 0, 0, 0, 0, 0, 0,
            BackgroundType.HVTiling, 255, false, false, 0, 0, null, false), true);
        using var red = new Bitmap(10, 10);
        using (var g = Graphics.FromImage(red)) g.Clear(Color.Red);
        var obj = new ObjectInfo(red, Point.Empty, "synthetic", "0", "0", "0", null);
        board.BoardItems.Add(obj.CreateInstance(board.Layers[0], board, 50, 25, 0, false), true);
        var front = new BackgroundInfo(null, blue, Point.Empty, "synthetic", BackgroundInfoType.Background, "1", null, null);
        board.BoardItems.Add(front.CreateInstance(board, 0, 0, 1, 0, 0, 0, 0,
            BackgroundType.Regular, 128, true, false, 0, 0, null, false), true);
        var result = MapAIVisualRenderer.RenderMap(board, new JObject
            { ["x"] = 0, ["y"] = 0, ["width"] = 100, ["height"] = 50, ["overlays"] = false });
        using var stream = new MemoryStream(Convert.FromBase64String((string)result[1]["data"]!));
        using var image = new Bitmap(stream);
        Assert.Equal(Color.Blue.ToArgb(), image.GetPixel(1, 1).ToArgb());
        Assert.Equal(Color.Blue.ToArgb(), image.GetPixel(98, 48).ToArgb());
        var blended = image.GetPixel(52, 27);
        Assert.InRange(blended.R, 126, 128);
        Assert.InRange(blended.B, 127, 129);
        Assert.Empty((JArray)JObject.Parse((string)result[0]["text"]!)["skippedItems"]!);
    });

    [Theory]
    [InlineData(0, 48)]
    [InlineData(-100, 18)]
    public void BackgroundCameraUsesCropAndNativeFlippedOrigin(int rx, int expectedLeft) => OnSta(() =>
    {
        var board = CreateBoard();
        using var art = new Bitmap(10, 10);
        using (var g = Graphics.FromImage(art))
        {
            g.Clear(Color.Blue);
            g.FillRectangle(Brushes.Red, 0, 0, 5, 10);
        }
        var info = new BackgroundInfo(null, art, new Point(2, 0), "synthetic", BackgroundInfoType.Background, "0", null, null);
        board.BoardItems.Add(info.CreateInstance(board, 6, 0, 0, rx, 0, 0, 0,
            BackgroundType.Regular, 255, false, true, 0, 0, null, false), true);
        var result = MapAIVisualRenderer.RenderMap(board, new JObject
            { ["x"] = -20, ["y"] = -25, ["width"] = 100, ["height"] = 50, ["overlays"] = false });
        using var stream = new MemoryStream(Convert.FromBase64String((string)result[1]["data"]!));
        using var image = new Bitmap(stream);
        Assert.Equal(Color.Blue.ToArgb(), image.GetPixel(expectedLeft + 1, 26).ToArgb());
        Assert.Equal(Color.Red.ToArgb(), image.GetPixel(expectedLeft + 8, 26).ToArgb());
    });

    [Fact]
    public void ArtworkUsesNativeOriginFlipAndLayerOrder() => OnSta(() =>
    {
        var board = CreateBoard();
        using var art = new Bitmap(20, 10);
        using (var g = Graphics.FromImage(art))
        {
            g.Clear(Color.Blue);
            g.FillRectangle(Brushes.Red, 0, 0, 10, 10);
        }
        var info = new ObjectInfo(art, new Point(3, 2), "synthetic", "0", "0", "0", null);
        board.BoardItems.Add(info.CreateInstance(board.Layers[0], board, 30, 20, 0, false), true);
        board.BoardItems.Add(info.CreateInstance(board.Layers[0], board, 70, 20, 0, true), true);
        using var topArt = new Bitmap(4, 4);
        using (var g = Graphics.FromImage(topArt)) g.Clear(Color.Lime);
        var top = new ObjectInfo(topArt, Point.Empty, "synthetic", "0", "0", "1", null);
        board.BoardItems.Add(top.CreateInstance(board.Layers[1], board, 30, 20, 0, false), true);
        var result = MapAIVisualRenderer.RenderMap(board, new JObject
        {
            ["x"] = 0, ["y"] = 0, ["width"] = 100, ["height"] = 50, ["overlays"] = false
        });
        using var stream = new MemoryStream(Convert.FromBase64String((string)result[1]["data"]!));
        using var image = new Bitmap(stream);
        Assert.Equal(Color.Red.ToArgb(), image.GetPixel(28, 19).ToArgb());
        Assert.Equal(Color.Blue.ToArgb(), image.GetPixel(44, 19).ToArgb());
        // Flipped editor instances shift their stored X to preserve the native anchor.
        Assert.Equal(Color.Blue.ToArgb(), image.GetPixel(54, 19).ToArgb());
        Assert.Equal(Color.Red.ToArgb(), image.GetPixel(71, 19).ToArgb());
        Assert.Equal(Color.Lime.ToArgb(), image.GetPixel(31, 21).ToArgb());
    });

    [Fact]
    public void HugeWorldCropHasBoundedOutputAndExactMapping() => OnSta(() =>
    {
        var board = CreateBoard();
        var result = MapAIVisualRenderer.RenderMap(board, new JObject
        {
            ["x"] = -100000, ["y"] = -200000, ["width"] = 1000000,
            ["height"] = 500000, ["maxDimension"] = 99999
        });
        var metadata = JObject.Parse((string)result[0]["text"]!);
        Assert.Equal(1000, (int)metadata["pixelWidth"]!);
        Assert.Equal(500, (int)metadata["pixelHeight"]!);
        Assert.Equal(0.001, (double)metadata["pixelsPerWorldUnit"]!, 9);
        Assert.Equal(-100000, (int)metadata["worldBounds"]!["x"]!);
        Assert.Throws<ArgumentException>(() => MapAIVisualRenderer.RenderMap(board, new JObject { ["x"] = 0 }));
        Assert.Throws<ArgumentException>(() => MapAIVisualRenderer.RenderMap(board,
            new JObject { ["x"] = 0, ["y"] = 0, ["width"] = 0, ["height"] = 1 }));
    });

    private static Board CreateBoard()
    {
        var board = new Board(new XnaPoint(200, 100), new XnaPoint(100, 50), new MultiBoard(),
            true, null, ItemTypes.All, ItemTypes.All);
        board.CreateMapLayers();
        return board;
    }

    private static void OnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Renderer test timed out");
        Assert.Null(failure);
    }
}
