using System.Drawing;
using System.IO;
using System.Threading;
using HaCreator;
using HaCreator.MapEditor;
using HaCreator.MapEditor.AI;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.Wz;
using MapleLib.Img;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System.Reflection;
using MapleLib.WzLib.WzStructure.Data;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;
using XnaPoint = Microsoft.Xna.Framework.Point;

namespace UnitTest_MapSimulator;

[CollectionDefinition("AI placement dataset", DisableParallelization = true)]
public class AIPlacementDatasetCollection { }

public sealed class V95PlacementFactAttribute : FactAttribute
{
    public V95PlacementFactAttribute()
    {
        if (!Directory.Exists(Environment.GetEnvironmentVariable("HACREATOR_AI_TEST_DATA")))
            Skip = "Set HACREATOR_AI_TEST_DATA to an exported v95 IMG directory.";
    }
}

[Collection("AI placement dataset")]
public class AIPlacementDatasetTests(ITestOutputHelper output)
{
    [V95PlacementFact]
    public void FirstPlatformsOnEmptyLayerRestoreMembershipAssetsAndPixels()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var previousInfo = Program.InfoManager;
            var previousSource = Program.DataSource;
            try
            {
                using var source = new ImgFileSystemDataSource(Environment.GetEnvironmentVariable("HACREATOR_AI_TEST_DATA")!);
                Program.DataSource = source;
                Program.InfoManager = new WzInformationManager();
                Program.InfoManager.TileSets["grassySoil"] = null;
                var parent = new MultiBoard();
                var board = new Board(new XnaPoint(1200, 800), new XnaPoint(600, 400), parent, true, null, ItemTypes.All, ItemTypes.All);
                board.CreateMapLayers();
                var layer = board.Layers[0];
                Assert.Null(layer.tS);
                Assert.Empty(layer.Items);
                var parser = new MapAIParser();
                var executor = new MapAIExecutor(board);
                string Render() => (string)MapAIVisualRenderer.RenderMap(board,
                    new JObject { ["x"] = -100, ["y"] = -200, ["width"] = 1000, ["height"] = 600 })[1]["data"]!;
                void Place(int start, int end, int y)
                {
                    Assert.True(executor.ExecuteCommand(parser.ParseCommand(
                        $"TILE PLATFORM tileset=\"grassySoil\" from x={start} to x={end} at y={y} layer=0")),
                        string.Join("\n", executor.ExecutionLog));
                }
                void AssertMembership(TileInstance[] expected)
                {
                    Assert.Equal("grassySoil", layer.tS);
                    Assert.Equal(expected.Length, layer.Items.Count);
                    Assert.Equal(expected.Length, board.BoardItems.TileObjs.Count);
                    Assert.All(expected, tile =>
                    {
                        Assert.Same(layer, tile.Layer);
                        Assert.Single(layer.Items.Where(item => ReferenceEquals(item, tile)));
                        Assert.Single(board.BoardItems.TileObjs.Where(item => ReferenceEquals(item, tile)));
                        Assert.NotNull(tile.BaseInfo);
                    });
                }
                Place(0, 360, 0);
                var first = board.BoardItems.TileObjs.OfType<TileInstance>().ToArray();
                Assert.NotEmpty(first);
                var firstAssets = first.Select(tile => tile.BaseInfo).ToArray();
                AssertMembership(first);
                string firstImage = Render();
                Place(450, 810, -100);
                var both = board.BoardItems.TileObjs.OfType<TileInstance>().ToArray();
                var bothAssets = both.Select(tile => tile.BaseInfo).ToArray();
                Assert.True(both.Length > first.Length);
                AssertMembership(both);
                string bothImage = Render();
                Assert.NotEqual(firstImage, bothImage);

                // Repeat the complete history cycle: stale layer members otherwise survive
                // the final undo and ReplaceTS(null) destroys the redo instances' asset info.
                for (int cycle = 0; cycle < 2; cycle++)
                {
                    board.UndoRedoMan.Undo();
                    AssertMembership(first);
                    Assert.Equal(firstImage, Render());
                    board.UndoRedoMan.Undo();
                    Assert.Null(layer.tS);
                    Assert.Empty(layer.Items);
                    Assert.Empty(board.BoardItems.TileObjs);
                    Assert.Empty(board.BoardItems.FHAnchors);
                    Assert.Empty(board.BoardItems.FootholdLines);
                    Assert.All(both, tile => Assert.NotNull(tile.BaseInfo));
                    board.UndoRedoMan.Redo();
                    AssertMembership(first);
                    for (int i = 0; i < first.Length; i++) Assert.Same(firstAssets[i], first[i].BaseInfo);
                    Assert.Equal(firstImage, Render());
                    board.UndoRedoMan.Redo();
                    AssertMembership(both);
                    for (int i = 0; i < both.Length; i++) Assert.Same(bothAssets[i], both[i].BaseInfo);
                    Assert.Equal(bothImage, Render());
                }
            }
            catch (Exception exception) { failure = exception; }
            finally { Program.InfoManager = previousInfo; Program.DataSource = previousSource; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(90)), "Empty-layer placement harness timed out");
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [V95PlacementFact]
    public void RealTilesRopesAndObjectsRemainUndoable()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var previousInfo = Program.InfoManager;
            var previousSource = Program.DataSource;
            try
            {
                using var source = new ImgFileSystemDataSource(Environment.GetEnvironmentVariable("HACREATOR_AI_TEST_DATA")!);
                Program.DataSource = source;
                Program.InfoManager = new WzInformationManager();
                Program.InfoManager.TileSets["grassySoil"] = null;
                Program.InfoManager.ObjectSets["connect"] = null;
                var parent = new MultiBoard();
                var board = new Board(new XnaPoint(1200, 800), new XnaPoint(600, 400), parent, true, null, ItemTypes.All, ItemTypes.All);
                board.CreateMapLayers();
                board.Layers[0].tS = "incompatible";
                board.Layers[2].tS = "grassySoil";
                var parser = new MapAIParser();
                var executor = new MapAIExecutor(board);
                Assert.True(executor.ExecuteCommand(parser.ParseCommand("TILE PLATFORM tileset=\"grassySoil\" from x=0 to x=360 at y=0 layer=0")), string.Join("\n", executor.ExecutionLog));
                Assert.NotEmpty(board.BoardItems.TileObjs);
                Assert.All(board.BoardItems.TileObjs.OfType<TileInstance>(), tile => Assert.Equal(2, tile.LayerNumber));
                Assert.NotEmpty(board.BoardItems.FootholdLines);
                Assert.All(board.BoardItems.FHAnchors, anchor => Assert.Equal(2, anchor.LayerNumber));
                Assert.All(board.BoardItems.FHAnchors, anchor => Assert.Equal(0, anchor.Y));
                Assert.All(board.BoardItems.TileObjs.OfType<TileInstance>().Where(tile => ((TileInfo)tile.BaseInfo).u == "enH0"),
                    tile => Assert.Equal(0, tile.Y + ((TileInfo)tile.BaseInfo).FootholdOffsets[0].Y));
                int tiles = board.BoardItems.TileObjs.Count;
                Assert.Single(board.UndoRedoMan.UndoList);
                foreach (var category in new[] { "enH0", "edU", "bsc", "enH1", "slLU", "slRU", "slLD", "slRD" })
                {
                    var info = TileInfo.Get("grassySoil", category, "0");
                    output.WriteLine($"{category}: {info?.Width}x{info?.Height}, origin={info?.Origin}, footholds={string.Join(";", info?.FootholdOffsets ?? [])}");
                }
                board.UndoRedoMan.Undo();
                Assert.Empty(board.BoardItems.TileObjs);
                Assert.Empty(board.BoardItems.FootholdLines);
                Assert.Empty(board.BoardItems.FHAnchors);
                board.UndoRedoMan.Redo();
                Assert.Equal(tiles, board.BoardItems.TileObjs.Count);
                Assert.True(executor.ExecuteCommand(parser.ParseCommand("ADD ROPE x=180 from y=100 to y=-30 layer=2")));
                Assert.Single(board.BoardItems.Ropes);
                Assert.Equal(-30, board.BoardItems.Ropes[0].FirstAnchor.Y);
                Assert.Equal(100, board.BoardItems.Ropes[0].SecondAnchor.Y);
                board.UndoRedoMan.Undo();
                Assert.Empty(board.BoardItems.Ropes);
                Assert.Empty(board.BoardItems.RopeLines);
                Assert.Empty(board.BoardItems.RopeAnchors);
                board.UndoRedoMan.Redo();
                Assert.Single(board.BoardItems.Ropes);
                int originalFootholds = board.BoardItems.FootholdLines.Count;
                int originalTiles = board.BoardItems.TileObjs.Count;
                foreach (string slopeType in new[] { "slope_up_right", "slope_up_left", "slope_down_right", "slope_down_left" })
                {
                    Assert.True(executor.ExecuteCommand(parser.ParseCommand($"TILE STRUCTURE type=\"{slopeType}\" tileset=\"grassySoil\" from x=400 to x=850 at y=100 height=3 layer=2")), string.Join("\n", executor.ExecutionLog));
                    var newLines = board.BoardItems.FootholdLines.Skip(originalFootholds)
                        .OrderBy(line => Math.Min(line.FirstDot.X, line.SecondDot.X)).ToList();
                    Assert.Equal(5, newLines.Count);
                    for (int i = 1; i < newLines.Count; i++)
                    {
                        var previousRight = newLines[i - 1].FirstDot.X > newLines[i - 1].SecondDot.X ? newLines[i - 1].FirstDot : newLines[i - 1].SecondDot;
                        var currentLeft = newLines[i].FirstDot.X < newLines[i].SecondDot.X ? newLines[i].FirstDot : newLines[i].SecondDot;
                        Assert.Equal(previousRight.X, currentLeft.X);
                        Assert.Equal(previousRight.Y, currentLeft.Y);
                    }
                    void AssertSavedChain()
                    {
                        var saver = new MapSaver(board);
                        var saved = new WzImage("ai-topology-test.img") { Parsed = true };
                        typeof(MapSaver).GetField("image", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(saver, saved);
                        saver.SaveFootholds();
                        var serialized = saved["foothold"]["2"].WzProperties.SelectMany(platform => platform.WzProperties)
                            .ToDictionary(foothold => int.Parse(foothold.Name));
                        for (int i = 0; i < newLines.Count; i++)
                        {
                            var data = serialized[newLines[i].num];
                            Assert.Equal(i == 0 ? 0 : newLines[i - 1].num, ((WzIntProperty)data["prev"]).Value);
                            Assert.Equal(i == newLines.Count - 1 ? 0 : newLines[i + 1].num, ((WzIntProperty)data["next"]).Value);
                        }
                    }
                    AssertSavedChain();
                    int slopeFootholds = board.BoardItems.FootholdLines.Count;
                    var slopeImage = MapAIVisualRenderer.RenderMap(board, new JObject { ["x"] = 350, ["y"] = -150, ["width"] = 550, ["height"] = 600 });
                    string slopePath = Path.Combine(Path.GetTempPath(), $"ai-{slopeType}-v95.png");
                    File.WriteAllBytes(slopePath, Convert.FromBase64String((string)slopeImage[1]["data"]!));
                    output.WriteLine(slopePath);
                    board.UndoRedoMan.Undo();
                    Assert.Equal(originalFootholds, board.BoardItems.FootholdLines.Count);
                    Assert.Equal(originalTiles, board.BoardItems.TileObjs.Count);
                    board.UndoRedoMan.Redo();
                    Assert.Equal(slopeFootholds, board.BoardItems.FootholdLines.Count);
                    AssertSavedChain();
                    board.UndoRedoMan.Undo();
                }
                var objects = Program.InfoManager.GetObjectSet("connect");
                ObjectInfo? objectInfo = null;
                foreach (var l0 in objects.WzProperties)
                foreach (var l1 in l0.WzProperties?.AsEnumerable() ?? Enumerable.Empty<MapleLib.WzLib.WzImageProperty>())
                foreach (var l2 in l1.WzProperties?.AsEnumerable() ?? Enumerable.Empty<MapleLib.WzLib.WzImageProperty>())
                {
                    if (l2["0"] == null) continue;
                    var candidate = ObjectInfo.Get("connect", l0.Name, l1.Name, l2.Name);
                    if (candidate?.FootholdOffsets?.Count > 0) { objectInfo = candidate; break; }
                }
                Assert.NotNull(objectInfo);
                string objectCommand = $"ADD OBJECT at (100, 0) oS=\"connect\" l0=\"{objectInfo.l0}\" l1=\"{objectInfo.l1}\" l2=\"{objectInfo.l2}\" layer=2 raw_position=false";
                output.WriteLine(objectCommand);
                Assert.True(executor.ExecuteCommand(parser.ParseCommand(objectCommand)), string.Join("\n", executor.ExecutionLog));
                var placedObject = Assert.Single(board.BoardItems.TileObjs.OfType<ObjectInstance>());
                Assert.Equal(0, placedObject.Y - placedObject.Origin.Y + placedObject.Height);
                Assert.True(board.BoardItems.FootholdLines.Count > originalFootholds);
                int objectFootholds = board.BoardItems.FootholdLines.Count;
                board.UndoRedoMan.Undo();
                Assert.Equal(originalFootholds, board.BoardItems.FootholdLines.Count);
                Assert.Empty(board.BoardItems.TileObjs.OfType<ObjectInstance>());
                board.UndoRedoMan.Redo();
                Assert.Equal(objectFootholds, board.BoardItems.FootholdLines.Count);
                Assert.Single(board.BoardItems.TileObjs.OfType<ObjectInstance>());
                Assert.False(executor.ExecuteCommand(parser.ParseCommand($"FLIP OBJECT at ({placedObject.X + 1}, {placedObject.Y})")));
                Assert.True(executor.ExecuteCommand(parser.ParseCommand($"FLIP OBJECT at ({placedObject.X}, {placedObject.Y})")));
                Assert.True(placedObject.Flip);
                board.UndoRedoMan.Undo();
                Assert.False(placedObject.Flip);
                int oldX = placedObject.X, oldY = placedObject.Y;
                Assert.True(executor.ExecuteCommand(parser.ParseCommand($"MOVE OBJECT at ({oldX}, {oldY}) to (300, 100) layer=2")));
                Assert.Equal(300, placedObject.X);
                Assert.Equal(100, placedObject.Y - placedObject.Origin.Y + placedObject.Height);
                board.UndoRedoMan.Undo();
                Assert.Equal(oldX, placedObject.X);
                Assert.Equal(oldY, placedObject.Y);
                Assert.True(executor.ExecuteCommand(parser.ParseCommand("CLEAR OBJECTS")));
                Assert.Empty(board.BoardItems.TileObjs.OfType<ObjectInstance>());
                board.UndoRedoMan.Undo();
                Assert.Single(board.BoardItems.TileObjs.OfType<ObjectInstance>());
                Assert.Equal(objectFootholds, board.BoardItems.FootholdLines.Count);
                Assert.True(executor.ExecuteCommand(parser.ParseCommand($"REMOVE OBJECT at ({placedObject.X}, {placedObject.Y})")));
                Assert.Empty(board.BoardItems.TileObjs.OfType<ObjectInstance>());
                board.UndoRedoMan.Undo();
                Assert.Single(board.BoardItems.TileObjs.OfType<ObjectInstance>());
                Assert.Equal(objectFootholds, board.BoardItems.FootholdLines.Count);
                board.UndoRedoMan.Redo();
                Assert.Empty(board.BoardItems.TileObjs.OfType<ObjectInstance>());
                Assert.Equal(originalFootholds, board.BoardItems.FootholdLines.Count);
                var assetSpecs = new JArray(new[] { "enH0", "edU", "bsc", "enH1" }.Select(category =>
                    new JObject { ["type"] = "tile", ["tS"] = "grassySoil", ["u"] = category, ["no"] = "0" }));
                var assets = MapAIVisualRenderer.RenderAssets(new JObject { ["assets"] = assetSpecs });
                var assetMetadata = JObject.Parse((string)assets[0]["text"]!);
                foreach (var asset in assetMetadata["assets"]!)
                {
                    var info = TileInfo.Get("grassySoil", (string)asset["asset"]!["u"]!, "0");
                    Assert.Equal(info.Width, (int)asset["width"]!);
                    Assert.Equal(info.Origin.Y, (int)asset["origin"]!["y"]!);
                }
                string assetPath = Path.Combine(Path.GetTempPath(), "ai-assets-v95.png");
                File.WriteAllBytes(assetPath, Convert.FromBase64String((string)assets[1]["data"]!));
                output.WriteLine(assetPath);
                var image = MapAIVisualRenderer.RenderMap(board, new JObject { ["x"] = -100, ["y"] = -100, ["width"] = 600, ["height"] = 350 });
                string imagePath = Path.Combine(Path.GetTempPath(), "ai-placement-v95.png");
                File.WriteAllBytes(imagePath, Convert.FromBase64String((string)image[1]["data"]!));
                output.WriteLine(imagePath);
                output.WriteLine(string.Join("\n", executor.ExecutionLog));
            }
            catch (Exception exception) { failure = exception; }
            finally { Program.InfoManager = previousInfo; Program.DataSource = previousSource; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(90)), "Placement harness timed out");
        Assert.Null(failure);
    }
}
